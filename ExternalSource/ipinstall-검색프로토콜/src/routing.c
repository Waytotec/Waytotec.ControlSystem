/******************************************************************** 
 * Copyright (c) 2006, Graham P Phillips
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without 
 * modification, are permitted provided that the following conditions 
 * are met:
 *
 *   * Redistributions of source code must retain the above copyright 
 *     notice, this list of conditions and the following disclaimer.
 *   * Redistributions in binary form must reproduce the above 
 *     copyright notice, this list of conditions and the following 
 *     disclaimer in the documentation and/or other materials provided 
 *     with the distribution.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 * "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS
 * FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE
 * COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT,
 * INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
 * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)
 * HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT,
 * STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED
 * OF THE POSSIBILITY OF SUCH DAMAGE.
 ********************************************************************/

#include <stdio.h>     /* for printf() */
#include <memory.h>    /* for memset() */
#include <time.h>      /* for time() */
#include <stdlib.h>    /* for exit() */
#include <unistd.h>    /* for close() */
#include <syslog.h>    /* for LOG_ERR */
#include <errno.h>     /* for errno */
#include <asm/types.h> /* for __u32 */
#include <sys/socket.h> /* for socket(), bind() */
#include <arpa/inet.h> /* for inet_ntoa() */
#include <linux/netlink.h> /* for NETLINK_ROUTE */
#include <linux/rtnetlink.h> /* for struct rtmsg */
#include "routing.h"


/***************************************************************************
 *  - Function prototypes
 ***************************************************************************/

int      list_add(struct route **list, struct route *route);
int      list_append(struct route **list, struct route *route);
int      send_route_request(struct rtsock *s);
int      recv_route_response2(struct rtsock *s,
  int (*doit) (struct sockaddr_nl *, struct nlmsghdr * n, void *),
  void *arg1);
int      convert_netlink2route(struct route *route,
  struct nlmsghdr *netlink_header);
int      parse_rtattr(struct rtattr *tb[], int max, struct rtattr *rta,
  int len);
int      addattr32(struct nlmsghdr *n, int maxlen, int type, __u32 data);
int      addattr_l(struct nlmsghdr *n, int maxlen, int type, void *data,
  int alen);
int      send_interface_request(struct rtsock *s);
int      write_route0(struct rtsock *s, struct route *route);
void     print_route(struct nlmsghdr *netlink_header);



/***************************************************************
 * open_route_socket(): Opens a netlink routing socket. Using 
 * this socket we can read and write routing information in the 
 * kernel. 
 *
 * Input:        none 
 * Output:       none 
 * Returns:      handle to rtsock structure. 
 * Effects:      Opens netlink kernel routing socket
 ***************************************************************/

struct rtsock *open_route_socket()
{
  int      rc;
  struct sockaddr_nl nladdr;

  struct rtsock *rts = NULL;

  if (!(rts = malloc(sizeof(struct rtsock)))) {
//    LOG(LOG_ERR, MN, E_No_Mem);
    exit(1);
  }

  rts->sock = socket(AF_NETLINK, SOCK_RAW, NETLINK_ROUTE);
  if (rts->sock < 0) {
//    LOG(LOG_ERR, MN, "socket() failed");
    return NULL;
  }
  memset(&nladdr, 0, sizeof(nladdr));
  nladdr.nl_family = AF_NETLINK;
  nladdr.nl_groups = 0;
  rc = bind(rts->sock, (struct sockaddr *) &nladdr, sizeof(nladdr));
  if (rc != 0) {
//    LOG(LOG_ERR, MN, "bind() failed");
    return NULL;
  }
  rts->seq = time(NULL);
  return rts;
}


/***************************************************************
 * close_route_socket(): Closes a netlink routing socket. 
 *
 * Input:        s -- socket handle 
 * Output:       none 
 * Returns:      0 on success, -1 otherwise 
 * Effects:      Closes netlink kernel routing socket
 ***************************************************************/

int close_route_socket(struct rtsock *s)
{
  if (s == NULL)
    return -1;
  return close(s->sock);
}



/***************************************************************
 * read_routes(): Reads the routes given the a netlink routing
 * socket.  The function is written to use a callback function
 * to do something useful with each route found.
 *
 * Input:        s -- socket handle 
 *               doit -- callback function
 *               arg -- argument that is passed to doit
 * Output:       none 
 * Returns:      0 on success, -1 otherwise 
 * Effects:      No side effects
 ***************************************************************/

int read_routes(struct rtsock *s,
  int (*doit) (struct sockaddr_nl *, struct nlmsghdr * n, void *), void *arg)
{
  int      rc = send_route_request(s);

  if (rc < 0) {
    printf("send_route_request() failed, rc = %d", rc);
    return -1;
  }
  rc = recv_route_response2(s, doit, arg);
  if (rc < 0) {
    printf("recv_route_response2() failed, rc = %d", rc);
    return -1;
  }
  return 0;
}


/***************************************************************
 * write_static_routes(): Adds the given set of routes into
 * the kernel's routing table for a particular interface. 
 *
 * Input:        s -- socket handle 
 *               routes -- list of routes 
 *               inf_index -- the index of the kernel interface 
 * Output:       none 
 * Returns:      0 on success, -1 otherwise 
 * Effects:      Modifies the kernel's routing table 
 ***************************************************************/

int write_static_routes(struct rtsock *s, struct route *routes, int inf_index)
{

  struct route *ptr;

  if (inf_index < 0)
    return -1;
  for (ptr = routes; ptr; ptr = ptr->next) {
    if (ptr->oif != inf_index)
      continue;
    if (!ptr->gateway_valid)
      continue;
    if (ptr->rtmsg.rtm_family != AF_INET)
      continue;
    if (ptr->rtmsg.rtm_table != RT_TABLE_MAIN)
      continue;
    if (ptr->rtmsg.rtm_protocol != RTPROT_BOOT)
      continue;
    if (ptr->rtmsg.rtm_scope != RT_SCOPE_UNIVERSE)
      continue;
    if (ptr->rtmsg.rtm_type != RTN_UNICAST)
      continue;
    if (write_route(s, ptr) < 0)
      return -1;
  }
  return 0;
}


/***************************************************************
 * filter_static_routes_on_inf(): Creates a new list by filtering 
 * all those routes in in_routes that are static routes for 
 * interface inf_index.  The routes that meet these criteria
 * are copied and prepended to out_routes.
 *
 * Input:        in_routes -- the list of routes to read 
 *               inf_index -- the index of the kernel interface 
 * Output:       out_routes -- new list of static routes 
 * Returns:      0 on success, -1 otherwise 
 * Effects:      No side effects. 
 ***************************************************************/

int filter_static_routes_on_inf(struct route *in_routes,
  struct route **out_routes, int inf_index)
{

  struct route *ptr;
  struct route *cprt;

  if (inf_index < 0)
    return -1;
  for (ptr = in_routes; ptr; ptr = ptr->next) {
    if (ptr->oif != inf_index)
      continue;
    if (!ptr->gateway_valid)
      continue;
    if (ptr->rtmsg.rtm_family != AF_INET)
      continue;
    if (ptr->rtmsg.rtm_table != RT_TABLE_MAIN)
      continue;
    if (ptr->rtmsg.rtm_protocol != RTPROT_BOOT)
      continue;
    if (ptr->rtmsg.rtm_scope != RT_SCOPE_UNIVERSE)
      continue;
    if (ptr->rtmsg.rtm_type != RTN_UNICAST)
      continue;

    if (!(cprt = (struct route *) malloc(sizeof(struct route)))) {
//      LOG(LOG_ERR, MN, E_No_Mem);
      exit(1);
    }
    memcpy(cprt, ptr, sizeof(struct route));
    cprt->next = NULL;
    list_add(out_routes, cprt);
  }
  return 0;
}


/***************************************************************
 * write_route(): Write a route to the kernel routing table. 
 *
 * Input:        s -- socket handle 
 *               route -- the route to be written 
 * Output:       none 
 * Returns:      0 on success, -1 otherwise 
 * Effects:      Modifies the kernel routing table 
 ***************************************************************/

int write_route(struct rtsock *s, struct route *route)
{

  fd_set   rset;
  struct timeval tv;

  FD_ZERO(&rset);
  FD_SET(s->sock, &rset);
  tv.tv_sec = 0;
  tv.tv_usec = 0;

  int      rc = write_route0(s, route);

  if (rc < 0) {
//    LOG(LOG_ERR, MN, "write_route0() failed, rc = %d", rc);
    return -1;
  }

  /* The kernel may respond with an NLMSG_ERROR message.  This may happen for
   * example if you attempt to write the default gateway when all the
   * interfaces are disabled.  If the kernel does so, we want to consume this
   * message before we send the next message.  We consume it and ignore it. */
  if (select(s->sock + 1, &rset, NULL, NULL, &tv) > 0) {
    rc = recv_route_response2(s, NULL, NULL);
    if (rc < 0) {
//      LOG(LOG_ERR, MN, "recv_route_response2() failed, rc = %d", rc);
      return -1;
    }
  }
  return 0;
}


/***************************************************************
 * write_route0(): Request to write a set a single route in
 * the kernel routing table. 
 *
 * Input:        s -- socket handle 
 *               route -- the route to be written 
 * Output:       none 
 * Returns:      0 on success, -1 otherwise 
 * Effects:      Modifies the kernel routing table 
 ***************************************************************/

int write_route0(struct rtsock *s, struct route *route)
{

  int      rc;

  struct
  {
    struct nlmsghdr netlink_header;
    struct rtmsg rt_message;
    char     space[1024];
  } request;

  struct sockaddr_nl nladdr;
  struct iovec iov = { &request, sizeof(request) };
  struct msghdr msg = {
    (void *) &nladdr, sizeof(nladdr),
    &iov, 1, NULL, 0, 0
  };

  if (route == NULL) {
//    LOG(LOG_ERR, MN, "write_route(), route parameter is NULL");
    return -1;
  }

  memset(&request, 0, sizeof(request));

  request.netlink_header.nlmsg_len = NLMSG_LENGTH(sizeof(struct rtmsg));
  request.netlink_header.nlmsg_flags =
    NLM_F_REQUEST | NLM_F_CREATE | NLM_F_REPLACE;
  request.netlink_header.nlmsg_type = RTM_NEWROUTE;

  request.rt_message.rtm_family = route->rtmsg.rtm_family;
  request.rt_message.rtm_table = route->rtmsg.rtm_table;

  request.rt_message.rtm_protocol = route->rtmsg.rtm_protocol;
  request.rt_message.rtm_scope = route->rtmsg.rtm_scope;
  request.rt_message.rtm_type = route->rtmsg.rtm_type;
  request.rt_message.rtm_dst_len = route->rtmsg.rtm_dst_len;

  if (route->gateway_valid) {
    rc =
      addattr_l(&request.netlink_header,
      sizeof(request) + sizeof(struct rtmsg), RTA_GATEWAY, &route->gateway,
      sizeof(struct in_addr));
    if (rc < 0) {
//      LOG(LOG_ERR, MN, "addattr_l() failed, rc = %d", rc);
      return -1;
    }
  }
  if (route->dst_valid) {
    rc =
      addattr_l(&request.netlink_header,
      sizeof(request) + sizeof(struct rtmsg), RTA_DST, &route->dst,
      sizeof(struct in_addr));
    if (rc < 0) {
//      LOG(LOG_ERR, MN, "addattr_l() failed, rc = %d", rc);
      return -1;
    }
  }
  if (route->oif >= 0) {
    rc =
      addattr32(&request.netlink_header,
      sizeof(request) + sizeof(struct rtmsg), RTA_OIF, route->oif);
    if (rc < 0) {
//      LOG(LOG_ERR, MN, "addattr_l() failed, rc = %d", rc);
      return -1;
    }
  }

  memset(&nladdr, 0, sizeof(nladdr));
  nladdr.nl_family = AF_NETLINK;

  request.netlink_header.nlmsg_seq = ++s->seq;

  iov.iov_len = request.netlink_header.nlmsg_len;

  rc = sendmsg(s->sock, &msg, 0);
  if (rc < 0) {
//    LOG(LOG_ERR, MN, "sendmsg() failed, rc = %d", rc);
    return -1;
  }
  return 0;
}




/***************************************************************
 * read_interfaces(): Read interface information from 
 * the kernel. The function is written to use a callback 
 * function to do something useful with each interface found.
 *
 * Input:        s -- socket handle 
 *               doit -- callback function
 *               arg -- argument that is passed to doit
 * Output:       none 
 * Returns:      0 on success, -1 otherwise 
 * Effects:      No side effects
 ***************************************************************/

int read_interfaces(struct rtsock *s,
  int (*doit) (struct sockaddr_nl *, struct nlmsghdr * n, void *), void *arg)
{
  int      rc = send_interface_request(s);

  if (rc < 0) {
//    LOG(LOG_ERR, MN, "send_interface_request() failed, rc = %d", rc);
    return -1;
  }

  rc = recv_route_response2(s, doit, arg);
  if (rc < 0) {
//    LOG(LOG_ERR, MN, "recv_route_response2() failed, rc = %d", rc);
    return -1;
  }
  return 0;
}




/***************************************************************
 * send_route_request(): Send a request to kernel to read the 
 * routes from kernel routing table.  The kernel will then 
 * respond with the set of routes.  See recv_route_response2().
 *
 * Input:        s -- socket handle 
 * Output:       none 
 * Returns:      0 on success, -1 otherwise 
 * Effects:      Sends request packet to kernel 
 ***************************************************************/

int send_route_request(struct rtsock *s)
{
  int      rc;
  struct sockaddr_nl nladdr;

  struct
  {
    struct nlmsghdr netlink_header;
    struct rtgenmsg rt_message;
  } request;

  struct iovec iov = { &request, sizeof(request) };
  struct msghdr msg = {
    (void *) &nladdr, sizeof(nladdr),
    &iov, 1, NULL, 0, 0
  };

  if (s == NULL)
    return -1;

  memset(&request, 0, sizeof(request));
  request.netlink_header.nlmsg_len = sizeof(request);
  request.netlink_header.nlmsg_type = RTM_GETROUTE;
  request.netlink_header.nlmsg_flags =
    NLM_F_ROOT | NLM_F_MATCH | NLM_F_REQUEST;
  request.netlink_header.nlmsg_pid = 0;
  request.netlink_header.nlmsg_seq = ++s->seq;

  request.rt_message.rtgen_family = AF_NETLINK;

  memset(&nladdr, 0, sizeof(nladdr));
  nladdr.nl_family = AF_NETLINK;
  nladdr.nl_pid = 0;
  nladdr.nl_groups = 0;

  rc = sendmsg(s->sock, &msg, 0);
  if (rc < 0) {
    return -1;
  }
  
  return 0;
}

#define RECV_BUF_LEN (1024*8)
char     recv_route_response_buf[RECV_BUF_LEN];


/***************************************************************
 * recv_route_response2(): Processes response packets sent by 
 * kernel containing a list of all the routes. 
 * The request for these packets was made by send_route_request(). 
 *
 * Input:        s -- socket handle 
 *               doit -- callback function
 *               arg -- argument that is passed to doit
 * Output:       none 
 * Returns:      0 on success, negative otherwise 
 * Effects:      No side effects
 ***************************************************************/

int recv_route_response2(struct rtsock *s,
  int (*doit) (struct sockaddr_nl *, struct nlmsghdr * n, void *), void *arg1)
{
  int      rc;
  struct nlmsghdr *netlink_header;
  struct sockaddr_nl nladdr;
  struct iovec iov =
    { &recv_route_response_buf, sizeof(recv_route_response_buf) };
  struct msghdr msg = {
    (void *) &nladdr, sizeof(nladdr),
    &iov, 1, NULL, 0, 0
  };

  if (s == NULL)
    return -1;

  while (1) {
    int      len;

    rc = recvmsg(s->sock, &msg, 0);
    if (rc <= 0) {
      return -2;
    }
    len = rc;
    if (msg.msg_namelen != sizeof(nladdr))
      return -3;
    if (nladdr.nl_family != AF_NETLINK)
      return -4;
    if (msg.msg_flags & MSG_TRUNC) {
      return -5;
    }
    if (msg.msg_flags & MSG_EOR) {
      return 0;
    }
    netlink_header = (struct nlmsghdr *) recv_route_response_buf;
    while (NLMSG_OK(netlink_header, len)) {
//        printf("recv_route_response2() expecting seq %d, found seq %d\n", s->seq,
//        netlink_header->nlmsg_seq);

      if (netlink_header->nlmsg_type == NLMSG_ERROR) {
        struct nlmsgerr *err = (struct nlmsgerr *) NLMSG_DATA(netlink_header);

        printf("netlink error %s\n", strerror(-err->error));
        if (err->error == -ENETUNREACH)
          return 0;
        return -6;
      }
      if (netlink_header->nlmsg_seq != s->seq) {
        return -7;
      }
      if (netlink_header->nlmsg_type == NLMSG_DONE)
        return 0;

      if (doit) {
        rc = doit(&nladdr, netlink_header, arg1);
        if (rc < 0)
          return -8;
      }
      netlink_header = NLMSG_NEXT(netlink_header, len);
    }
  }
  return 0;
}




/***************************************************************
 * convert_netlink2route(): Converts a kernel route data 
 * structure into a 'struct route' data structure. 
 *
 * Input:        netlink_header -- netlink route 
 * Output:       route -- converted route 
 * Returns:      0 on success, -1 otherwise 
 * Effects:      No side effects
 ***************************************************************/

int convert_netlink2route(struct route *route,
  struct nlmsghdr *netlink_header)
{

  int      rc;
  struct rtmsg *rt = NLMSG_DATA(netlink_header);
  struct rtattr *tb[RTA_MAX + 1];

  memset(route, 0, sizeof(struct route));
  route->oif = -1;

  memcpy(&route->rtmsg, rt, sizeof(struct rtmsg));

  memset(tb, 0, sizeof(tb));
  rc =
    parse_rtattr(tb, RTA_MAX, RTM_RTA(rt),
    netlink_header->nlmsg_len - sizeof(struct nlmsghdr) -
    sizeof(struct rtmsg));
  if (rc < 0)
    return -1;

  if (tb[RTA_GATEWAY]) {
    memcpy(&route->gateway, RTA_DATA(tb[RTA_GATEWAY]),
      sizeof(struct in_addr));
    route->gateway_valid = 1;
  }
  if (tb[RTA_DST]) {
    memcpy(&route->dst, RTA_DATA(tb[RTA_DST]), sizeof(struct in_addr));
    route->dst_valid = 1;
  }
  if (tb[RTA_OIF]) {
    route->oif = *(int *) RTA_DATA(tb[RTA_OIF]);
  }
  return 0;
}



/***************************************************************
 * send_interface_request(): Send a request to kernel to read 
 * interface information from the kernel routing table.  
 *
 * Input:        s -- socket handle 
 * Output:       none 
 * Returns:      0 on success, -1 otherwise (see sendto)
 * Effects:      No side effects
 ***************************************************************/

int send_interface_request(struct rtsock *s)
{
  struct sockaddr_nl nladdr;
  struct
  {
    struct nlmsghdr nlh;
    struct rtgenmsg rgm;
  } req;

  memset(&nladdr, 0, sizeof(nladdr));
  nladdr.nl_family = AF_NETLINK;

  req.nlh.nlmsg_len = sizeof(req);
  req.nlh.nlmsg_type = RTM_GETLINK;
  req.nlh.nlmsg_flags = NLM_F_ROOT | NLM_F_MATCH | NLM_F_REQUEST;
  req.nlh.nlmsg_pid = 0;
  req.nlh.nlmsg_seq = ++s->seq;
  req.rgm.rtgen_family = AF_UNSPEC;

  return sendto(s->sock, (void *) &req, sizeof(req), 0,
    (struct sockaddr *) &nladdr, sizeof(nladdr));
}



/***************************************************************
 * list_add(): Add a route object to the front of the given list
 *
 * Input:        list -- list of routes 
 * Output:       route -- route to add 
 * Returns:      0 
 * Effects:      No side effects
 ***************************************************************/

int list_add(struct route **list, struct route *route)
{
  route->next = *list;
  *list = route;
  return 0;
}


/***************************************************************
 * list_append(): Add a route object to the end of 
 * the given list.  Note that route->next is not modified, so 
 * make sure that it is valid. 
 *
 * Input:        list -- list of routes 
 * Output:       route -- route to add 
 * Returns:      0 
 * Effects:      No side effects
 ***************************************************************/

int list_append(struct route **list, struct route *route)
{

  if (!*list)
    *list = route;
  else {
    struct route *ptr;

    for (ptr = *list; ptr->next; ptr = ptr->next);
    ptr->next = route;
  }
  route->next = NULL;
  return 0;
}

/***************************************************************
 * list_delete(): Delete the entire list of route objects from 
 * the given list.
 *
 * Input:        list -- list of routes 
 * Returns:      none 
 * Effects:      No side effects
 ***************************************************************/

void list_delete(struct route **list)
{

  if (*list != NULL) {
    struct route *ptr, *prev_ptr;

    ptr = *list;
    do {
      prev_ptr = ptr;
      ptr = ptr->next;
      free(prev_ptr);
    } while (ptr != NULL);
    *list = NULL;
  }
  return;
}


/***************************************************************
 * route_append(): A callback function to convert a netlink 
 * route to a 'struct route' object and append it to a list of 
 * routes.
 *
 * Input:        who -- netlink socket 
 *               nlmsghdr -- netlink message 
 *               arg -- pointer to the head of the list of routes 
 * Output:       none 
 * Returns:      0 on success, -1 on failure 
 * Effects:      No side effects. 
 ***************************************************************/

int route_append(struct sockaddr_nl *who, struct nlmsghdr *n, void *arg)
{
  struct route *route;
  int      rc;

  if (!(route = (struct route *) malloc(sizeof(struct route)))) {
//    LOG(LOG_ERR, MN, E_No_Mem);
    exit(1);
  }
  rc = convert_netlink2route(route, n);
  if (rc < 0) {
    printf("convert_netlink2route() failed, rc = %d", rc);
    return -1;
  }
  list_append((struct route **) arg, route);

  return 0;
}


/***************************************************************
 * rtm_type(): Pretty-print the rtm_type in a netlink datagram.
 * Used for debugging.
 * 
 * Input:        type -- rtm_type
 * Output:       none 
 * Returns:      Pretty-print string 
 * Effects:      No side effects. 
 ***************************************************************/

char    *rtm_type(unsigned char type)
{
  switch (type) {
    case RTN_UNSPEC:
      return "RTN_UNSPEC";
    case RTN_UNICAST:
      return "RTN_UNICAST";
    case RTN_LOCAL:
      return "RTN_LOCAL";
    case RTN_BROADCAST:
      return "RTN_BROADCAST";
    case RTN_ANYCAST:
      return "RTN_ANYCAST";
    case RTN_MULTICAST:
      return "RTN_MULTICAST";
    case RTN_BLACKHOLE:
      return "RTN_BLACKHOLE";
    case RTN_UNREACHABLE:
      return "RTN_UNREACHABLE";
    case RTN_PROHIBIT:
      return "RTN_PROHIBIT";
    case RTN_THROW:
      return "RTN_THROW";
    case RTN_NAT:
      return "RTN_NAT";
    case RTN_XRESOLVE:
      return "RTN_XRESOLVE";
  }
  return "???";
}


/***************************************************************
 * rtm_protocol(): Pretty-print the rtm_protocol (the protocol
 * for the route). Used for debugging.
 * 
 * Input:        protocol -- rtm_protocol 
 * Output:       none 
 * Returns:      Pretty-print string 
 * Effects:      No side effects. 
 ***************************************************************/

char    *rtm_protocol(unsigned char protocol)
{
  switch (protocol) {
    case RTPROT_UNSPEC:
      return "RTPROT_UNSPEC";
    case RTPROT_REDIRECT:
      return "RTPROT_REDIRECT";
    case RTPROT_KERNEL:
      return "RTPROT_KERNEL";
    case RTPROT_BOOT:
      return "RTPROT_BOOT";
    case RTPROT_STATIC:
      return "RTPROT_STATIC";
  }
  return "???";
}


/***************************************************************
 * rtm_table(): Pretty-print the rtm_table ID (routing table 
 * ID).  Used for debugging.
 * 
 * Input:        table -- routing table ID 
 * Output:       none 
 * Returns:      Pretty-print string 
 * Effects:      No side effects. 
 ***************************************************************/

char    *rtm_table(unsigned char table)
{
  switch (table) {
    case RT_TABLE_UNSPEC:
      return "RT_TABLE_UNSPEC";
    case RT_TABLE_DEFAULT:
      return "RT_TABLE_DEFAULT";
    case RT_TABLE_MAIN:
      return "RT_TABLE_MAIN";
    case RT_TABLE_LOCAL:
      return "RT_TABLE_LOCAL";
  }
  return "???";
}

/***************************************************************
 * rtm_scope(): Pretty-print the rtm_scope (the scope of the
 * route).  Used for debugging.
 * 
 * Input:        table -- routing table ID 
 * Output:       none 
 * Returns:      Pretty-print string 
 * Effects:      No side effects. 
 ***************************************************************/

char    *rtm_scope(unsigned char scope)
{
  switch (scope) {
    case RT_SCOPE_UNIVERSE:
      return "RT_SCOPE_UNIVERSE";
    case RT_SCOPE_SITE:
      return "RT_SCOPE_SITE";
    case RT_SCOPE_LINK:
      return "RT_SCOPE_LINK";
    case RT_SCOPE_HOST:
      return "RT_SCOPE_HOST";
    case RT_SCOPE_NOWHERE:
      return "RT_SCOPE_NOWHERE";
  }
  return "???";
}


/***************************************************************
 * rta_type(): Pretty-print the rta_type (the type of the route
 * attribute).  Used for debugging.
 * 
 * Input:        type -- routing attribute type 
 * Output:       none 
 * Returns:      Pretty-print string 
 * Effects:      No side effects. 
 ***************************************************************/

char    *rta_type(unsigned short type)
{
  switch (type) {
    case RTA_UNSPEC:
      return "RTA_UNSPEC";
    case RTA_DST:
      return "RTA_DST";
    case RTA_SRC:
      return "RTA_SRC";
    case RTA_IIF:
      return "RTA_IIF";
    case RTA_OIF:
      return "RTA_OIF";
    case RTA_GATEWAY:
      return "RTA_GATEWAY";
    case RTA_PRIORITY:
      return "RTA_PRIORITY";
    case RTA_PREFSRC:
      return "RTA_PREFSRC";
    case RTA_METRICS:
      return "RTA_METRICS";
    case RTA_MULTIPATH:
      return "RTA_MULTIPATH";
    case RTA_PROTOINFO:
      return "RTA_PROTOINFO";
    case RTA_FLOW:
      return "RTA_FLOW";
    case RTA_CACHEINFO:
      return "RTA_CACHEINFO";
  }
  return "???";
}


/***************************************************************
 * print_route(): Pretty-print the netlink route.  Used for 
 * debugging.
 * 
 * Input:        netlink_header -- netlink route 
 * Output:       none 
 * Returns:      none 
 * Effects:      Pretty-print the route information to stdout. 
 ***************************************************************/

void print_route(struct nlmsghdr *netlink_header)
{

  struct rtmsg *rt = NLMSG_DATA(netlink_header);
  struct rtattr *tb[RTA_MAX + 1];
  int      i;

  printf("nlmsg_len=%d\n", netlink_header->nlmsg_len);
  printf("rtm_dst_len=%u\n", rt->rtm_dst_len);
  printf("rtm_table=%s\n", rtm_table(rt->rtm_table));
  printf("rtm_protocol=%s\n", rtm_protocol(rt->rtm_protocol));
  printf("rtm_scope=%s\n", rtm_scope(rt->rtm_scope));
  printf("rtm_type=%s\n", rtm_type(rt->rtm_type));

  memset(tb, 0, sizeof(tb));

  parse_rtattr(tb, RTA_MAX, RTM_RTA(rt),
    netlink_header->nlmsg_len - sizeof(struct nlmsghdr) -
    sizeof(struct rtmsg));

  for (i = 0; i <= RTA_MAX; i++) {
    if (tb[i])
      printf("rta_type=%s\n", rta_type(tb[i]->rta_type));
  }

  if (tb[RTA_DST]) {
    struct in_addr *sin_addr = RTA_DATA(tb[RTA_DST]);

    printf("RTA_DST = %s\n", inet_ntoa(*sin_addr));
  }
  if (tb[RTA_OIF]) {
    int      oif = *(int *) RTA_DATA(tb[RTA_OIF]);

    printf("RTA_OIF = %d\n", oif);
  }
  if (tb[RTA_GATEWAY]) {
    struct in_addr *sin_addr = RTA_DATA(tb[RTA_GATEWAY]);

    printf("RTA_GATEWAY = %s\n", inet_ntoa(*sin_addr));
  }
  if (tb[RTA_PREFSRC]) {
    struct in_addr *sin_addr = RTA_DATA(tb[RTA_PREFSRC]);

    printf("RTA_PREFSRC = %s\n", inet_ntoa(*sin_addr));
  }
  printf("\n");
}


/***************************************************************
 * addattr32(): Append attribute to netlink datagram. 
 * 
 * Input:        n -- netlink datagram 
 *               maxlen -- length of the allocated datagram (in bytes)
 *               type -- routing attribute type (rta_type) 
 *               __u32 -- routing attribute to append
 * Output:       none 
 * Returns:      none 
 * Effects:      No side effects 
 ***************************************************************/

int addattr32(struct nlmsghdr *n, int maxlen, int type, __u32 data)
{
  int      len = RTA_LENGTH(4);
  struct rtattr *rta;

  if (NLMSG_ALIGN(n->nlmsg_len) + len > maxlen)
    return -1;
  rta = (struct rtattr *) (((char *) n) + NLMSG_ALIGN(n->nlmsg_len));
  rta->rta_type = type;
  rta->rta_len = len;
  memcpy(RTA_DATA(rta), &data, 4);
  n->nlmsg_len = NLMSG_ALIGN(n->nlmsg_len) + len;
  return 0;
}

/***************************************************************
 * addattr_l(): Append attribute to netlink datagram. 
 * 
 * Input:        n -- netlink datagram 
 *               maxlen -- length of the allocated datagram (in bytes)
 *               type -- routing attribute type (rta_type) 
 *               data -- routing attribute to append
 *               alen -- length of attribute (in bytes)
 * Output:       none 
 * Returns:      none 
 * Effects:      No side effects 
 ***************************************************************/

int addattr_l(struct nlmsghdr *n, int maxlen, int type, void *data, int alen)
{
  int      len = RTA_LENGTH(alen);
  struct rtattr *rta;

  if (NLMSG_ALIGN(n->nlmsg_len) + len > maxlen)
    return -1;
  rta = (struct rtattr *) (((char *) n) + NLMSG_ALIGN(n->nlmsg_len));
  rta->rta_type = type;
  rta->rta_len = len;
  memcpy(RTA_DATA(rta), data, alen);
  n->nlmsg_len = NLMSG_ALIGN(n->nlmsg_len) + len;
  return 0;
}


/***************************************************************
 * addatr32(): Parse a routing attribute.    The tb array is 
 * assumed to have been cleared before calling this function.
 * This function will then populate some of the array elements. 
 * 
 * Input:        max -- max number of items in tb[] 
 *               rta -- the route attribute to parse 
 *               len -- the length of the route attribute (in bytes)
 * Output:       tb -- array of all the possible routing attributes  
 * Returns:      0 
 * Effects:      No side effects 
 ***************************************************************/

int parse_rtattr(struct rtattr *tb[], int max, struct rtattr *rta, int len)
{
  while (RTA_OK(rta, len)) {
    if (rta->rta_type <= max)
      tb[rta->rta_type] = rta;
    rta = RTA_NEXT(rta, len);
  }
  if (len)
    printf("!!!Deficit %d, rta_len=%d\n", len, rta->rta_len);
  
  return 0;
}
