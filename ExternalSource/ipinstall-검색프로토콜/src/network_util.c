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
#include <stdio.h>
#include <stdlib.h>        /* for malloc, free */
#include <stdint.h>        /* for uint32_t */
#include <stddef.h>        /* for offsetof */
#include <string.h>        /* for strcmp */
#include <syslog.h>        /* for log levels */
#include <unistd.h>        /* for gethostname */
#include <errno.h>         /* for errno, EINVAL */
#include <asm/types.h>     /* for linux/netlink.h */
#include <sys/ioctl.h>     /* for SIOCGIFCONF */
#include <arpa/inet.h>     /* for inet_ntoa */
#include <net/if.h>        /* for IFNAMSIZ, ifreq */
#include <netinet/in.h>    /* for sockaddr */
#include <linux/netlink.h> /* for sockaddr_nl */

#include "network_util.h"

#include "routing.h"


/***************************************************************************
 *  - Variable allocation and initialization
 ***************************************************************************/
static int Skfd = -1;   /* Global handle to kernel socket */
static struct rtsock *KernelRoutingSocket = NULL; /* Global handle to a kernel routing socket */

static Interface *InterfaceHead = NULL;
static HostRow HostTable[1];

static int SerialLoadFlag = 0;
static int SerialLoad[MAC_ADDRESS_MAX_LEN];

/***************************************************************************
 *  - Function prototypes
 ***************************************************************************/
 
int openSocket()
{
    if(Skfd != -1)
        return Skfd;
    
    Skfd = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
    return Skfd;
}

void closeSocket()
{
    if(Skfd == -1)
        return;
    
    close(Skfd);
    Skfd = -1;
}

/***************************************************************
 * readInterfaceNames():  - Read the names of all ethernet 
 * interfaces from the kernel.  For each name, the doit callback 
 * function is called with the name in a ifreq structure.  In 
 * particular, the interface name is given in the ifr_name field 
 * in the ifreq structure.  Typically it is the callback 
 * function that would do something useful.
 *
 * Input:       skfd -- a socket for reading interface 
 *              information from the kernel
 *              doit -- a callback function 
                cookie -- not used
 * Return:      0 
 * Effects:     None, other than the effects of the callback function. 
 **************************************************************/

int
readInterfaceNames(int skfd, int (*doit) (struct ifreq *, void *),
  void *cookie)
{
  int      ifnum = 32;
  int      lastlen = 0;
  struct ifconf ifc;
  char    *ptr;

  ifc.ifc_buf = NULL;
  for (;;) {
    ifc.ifc_len = ifnum * sizeof(struct ifreq);
    if (ifc.ifc_buf != NULL)
      free(ifc.ifc_buf);
    if (!(ifc.ifc_buf = (char *) malloc(ifc.ifc_len))) {
//      LOG(LOG_ERR, MN, "malloc() failed, %s", strerror(errno));
      exit(1);
    }
    if (ioctl(skfd, SIOCGIFCONF, (caddr_t) & ifc) < 0) {
      if (errno != EINVAL || lastlen != 0) {
        free(ifc.ifc_buf);
      }
    }
    else {
      if (ifc.ifc_len == lastlen)
        break;
      lastlen = ifc.ifc_len;
    }
    ifnum += 32;
  }

  for (ptr = ifc.ifc_buf; ptr < ifc.ifc_buf + ifc.ifc_len;) {
    int      len;
    struct ifreq *ifr;

    ifr = (struct ifreq *) ptr;
    switch (ifr->ifr_addr.sa_family) {
      case AF_INET:
        len = sizeof(struct sockaddr_in);
        break;

      case AF_INET6:
        len = sizeof(struct sockaddr_in6);
        break;

      default:
//        LOG(LOG_ERR, MN, "Don't know how to process family %d",
//          ifr->ifr_addr.sa_family);
        printf("Don't know how to process family %d",ifr->ifr_addr.sa_family);
    }
    ptr += sizeof(ifr->ifr_name) + len;
    if (doit)
      doit(ifr, cookie);
  }
  return 0;
}

/***************************************************************
 * addToInterfaceTable(): - This function is responsible for
 * initializing the RTA Interface data structure. Each call adds
 * one Inteface object to the global the RTA Interface data 
 * structure.  This method is used as a call-back function by 
 * the readInterfaceNames() function.
 *
 * Input:       struct ifreq *ifr -- pointer to 
 *              network interface data-structure (only 
 *              ifr->ifr_name is assumed to be initialized).
 *              void *cookie -- not used
 * Output:      struct interface *ifr -- treated as a scratch pad,
 *              some variables in ifr are populated 
 * Effects:     Modifies the global RTA Interface data-structure
 ***************************************************************/
int addToInterfaceTable(struct ifreq *ifr, void *cookie)
{
  Interface *inf;
  struct sockaddr_in *sin;

  /* At this point, only ifr->ifr_name is assumed to be initialized */

  if (!(inf = (Interface *) calloc(1, sizeof(Interface)))) {
//    LOG(LOG_ERR, MN, "calloc() failed, %s", strerror(errno));
    exit(1);
  }

  strncpy(inf->name, ifr->ifr_name, IFNAMSIZ);

  if (ioctl(Skfd, SIOCGIFFLAGS, &ifr) >= 0) {
    inf->status = (ifr->ifr_flags & IFF_RUNNING) ? 1 : 0;
  }
  else {
//    LOG(LOG_ERR, MN, "ioctl() failed, %s", strerror(errno));
    inf->status = 0;
  }

  if (ioctl(Skfd, SIOCGIFADDR, &ifr) >= 0) {
    sin = (struct sockaddr_in *) &ifr->ifr_addr;
    strncpy(inf->IpAddress, inet_ntoa(sin->sin_addr),
      IP_ADDRESS_MAX_LEN);
  }
  else {
//    LOG(LOG_ERR, MN, "ioctl() failed while reading IP address, %s", strerror(errno));
    memset(inf->IpAddress, 0, IP_ADDRESS_MAX_LEN);
  }
  if ((ioctl(Skfd, SIOCGIFBRDADDR, &ifr)) >= 0) {
    sin = (struct sockaddr_in *) &ifr->ifr_broadaddr;
    strncpy(inf->Broadcast, inet_ntoa(sin->sin_addr),
      IP_ADDRESS_MAX_LEN);
  }
  else {
//    LOG(LOG_ERR, MN, "ioctl() failed while reading broadcast address , %s", strerror(errno));
    memset(inf->Broadcast, 0, IP_ADDRESS_MAX_LEN);
  }
  if ((ioctl(Skfd, SIOCGIFNETMASK, &ifr)) >= 0) {
    sin = (struct sockaddr_in *) &ifr->ifr_netmask;
    strncpy(inf->Mask, inet_ntoa(sin->sin_addr),
      IP_ADDRESS_MAX_LEN);
  }
  else {
//    LOG(LOG_ERR, MN, "ioctl() failed while reading netmask, %s", strerror(errno));
    memset(inf->Mask, 0, IP_ADDRESS_MAX_LEN);
  }

  inf->kidx = -1;

  /* add to the front of our interface list */
  inf->next = InterfaceHead;
  InterfaceHead = inf;
  return 0;
}


/***************************************************************
 * getNextInterface(): - an 'iterator' on the linked list of 
 * interfaces, whose head pointer is InterfaceHead. 
 *
 * Input:        void *prow  -- pointer to current row
 *               void *it_data -- callback data.  Unused.
 *               int   rowid -- the row number.  Unused.
 * Output:       pointer to next row.  NULL on last row
 * Effects:      No side effects
 ***************************************************************/
void    *getNextInterface(void *prow, void *it_data, int rowid)
{
  if (prow == (void *) NULL)
    return ((void *) InterfaceHead);
  return ((void *) ((Interface *) prow)->next);
}


/***************************************************************
 * writeAddress(): Set the IP address, netmask or broadcast 
 * address of a given interface in the kernel.  This function is 
 * used by the RTA callback functions to modify the kernel's 
 * interfaces whenever a user modifies an address in the Interface 
 * table. 
 *
 * Input:       ifname -- the name of the kernel interface 
 *              flag -- one of SIOCSIFADDR for the ip address, 
 *                     SIOCSIFNETMASK for netmask, or 
 *                     SIOCSIFBRDADDR for broadcast. 
 *              addr -- the IP address 
 * Return:      0 on success, -1 on failure
 * Effects:     Updates the kernel's interface table. 
 **************************************************************/
int writeAddress(char *ifname, int flag, struct in_addr *addr)
{

  struct sockaddr_in sin;
  struct ifreq ifr;

  sin.sin_family = AF_INET;
  sin.sin_port = 0;
  memcpy(&sin.sin_addr, addr, sizeof(struct in_addr));

  memset(&ifr, 0, sizeof(ifr));
  strncpy(ifr.ifr_name, ifname, IFNAMSIZ);
  memcpy((char *) &ifr.ifr_addr, (char *) &sin, sizeof(struct sockaddr_in));

  if (ioctl(Skfd, flag, &ifr) < 0) {
//    LOG(LOG_ERR, MN, "ioctl() failed, %s", strerror(errno));
    return -1;
  }
  return 0;
}

/***************************************************************
 * readIpAddress(): Read the IP address from a given 
 * kernel named interface.  This function is 
 * used by the RTA callback functions to read the kernel's 
 * interfaces whenever a user reads an address in the Interface 
 * table. 
 *
 * Input:       ifname -- the name of the kernel interface 
 * Output:      inf -- the address is converted to a string 
 *              and placed in inf->IpAddress.
 * Return:      none 
 * Effects:     No side effects
 **************************************************************/
void readIpAddress(char *ifname, Interface * inf)
{

  struct sockaddr_in sin;
  struct ifreq ifr;
  char    *addr;

  memset(&ifr, 0, sizeof(ifr));
  strncpy(ifr.ifr_name, ifname, IFNAMSIZ);

  if(openSocket() == -1) {
     printf("socket open error\n");
     return;
  }

  /* SIOCGIFADDR means 'read IP address' */
  if (ioctl(Skfd, SIOCGIFADDR, &ifr) < 0) {
     return;
  }

  memcpy((char *) &sin, (char *) &ifr.ifr_addr, sizeof(struct sockaddr_in));
  addr = inet_ntoa(sin.sin_addr);
  if (!addr) {
     return;
  }
  strncpy(inf->IpAddress, addr, IFNAMSIZ);

  closeSocket();
}


/***************************************************************
 * readNetmask(): Read the netmask address for the given kernel
 * named interface. 
 *
 * Input:       ifname -- the name of the kernel interface 
 * Output:      inf -- the address is converted to a string 
 *              and placed in inf->Mask.
 * Return:      none 
 * Effects:     No side effects
 **************************************************************/
void readNetmask(char *ifname, Interface * inf)
{

  struct sockaddr_in sin;
  struct ifreq ifr;
  char    *addr;
  int      rc;

  memset(&ifr, 0, sizeof(ifr));
  strncpy(ifr.ifr_name, ifname, IFNAMSIZ);

  if(openSocket() == -1) {
     printf("socket open error\n");
     return;
  }

  /* SIOCGIFNETMASK means 'read netmask' */
  if ((rc = ioctl(Skfd, SIOCGIFNETMASK, &ifr)) < 0) {
//    LOG(LOG_ERR, MN, "ioctl() failed while reading netmask, %s", strerror(errno));
    return;
  }

  memcpy((char *) &sin, (char *) &ifr.ifr_addr, sizeof(struct sockaddr_in));
  addr = inet_ntoa(sin.sin_addr);
  if (!addr) {
//    LOG(LOG_ERR, MN, "inet_ntoa() failed while reading netmask");
    return;
  }
  strncpy(inf->Mask, addr, IFNAMSIZ);

  closeSocket();
}
void readMacAddress(char *ifname, Interface * inf)
{

  struct sockaddr_in sin;
  struct ifreq ifr;
  char    *addr;

  memset(&ifr, 0, sizeof(ifr));
  strncpy(ifr.ifr_name, ifname, IFNAMSIZ);

  if(openSocket() == -1) {
     printf("socket open error\n");
     return;
  }

  if ((ioctl(Skfd, SIOCGIFHWADDR, &ifr)) < 0) {
//    LOG(LOG_ERR, MN, "ioctl() failed while reading hw address,%s: %s", inf->name,strerror(errno));
    memset(inf->MacAddress, 0, MAC_ADDRESS_MAX_LEN);
    return;
  }
  sprintf(inf->MacAddress,"%02x:%02x:%02x:%02x:%02x:%02x",
                           ifr.ifr_addr.sa_data[0],
                           ifr.ifr_addr.sa_data[1],
                           ifr.ifr_addr.sa_data[2],
                           ifr.ifr_addr.sa_data[3],
                           ifr.ifr_addr.sa_data[4],
                           ifr.ifr_addr.sa_data[5]);

  closeSocket();
}
/***************************************************************
 * readGateway(): Read the gateway address from the kernel 
 * routing table.
 *
 * Input:       none 
 * Output:      h -- the gateway address is converted to a string 
 *              and placed in h->gateway. 
 * Return:      0 on success, -1 on failure 
 * Effects:     No side effects
 **************************************************************/
int readGateway(HostRow * h)
{
  int      rc;
  struct route *routes = NULL;
  struct route *ptr;

  memset(h->gateway, 0, IP_ADDRESS_MAX_LEN);
  if ((KernelRoutingSocket = open_route_socket()) == NULL) {
    printf("open_route_socket() failed");
    return -1;  
  }

  rc = read_routes(KernelRoutingSocket, route_append, (void *) &routes);
  if (rc < 0) {
    printf("read_routes() failed");
    goto cleanup;
  }
  /* Don't signal error if there is no gateway */
  rc = 0;
  for (ptr = routes; ptr; ptr = ptr->next) {
    if (ptr->gateway_valid && ptr->rtmsg.rtm_table == RT_TABLE_MAIN) {
      strncpy(h->gateway, inet_ntoa(ptr->gateway), IP_ADDRESS_MAX_LEN);
      break;
    }
  }
cleanup:
  list_delete(&routes);
  close_route_socket(KernelRoutingSocket);  
  return rc;
}


/***************************************************************
 * writeGateway(): Set the gateway address in the kernel 
 * routing table.
 *
 * Input:       h -- the gateway address given by the string in
 *              h->gateway is converted and sent as a routing request
 *              to the kernel.   
 * Return:      0 on success, -1 on failure 
 * Effects:     Updates kernel routing table.
 **************************************************************/
int writeGateway(HostRow * h)
{
  struct route r;

  memset(&r, 0, sizeof(struct route));

  r.gateway_valid = 1;
  r.oif = -1;
  r.rtmsg.rtm_family = AF_INET;
  r.rtmsg.rtm_table = RT_TABLE_MAIN;
  r.rtmsg.rtm_protocol = RTPROT_BOOT;
  r.rtmsg.rtm_scope = RT_SCOPE_UNIVERSE;
  r.rtmsg.rtm_type = RTN_UNICAST;

  if (inet_aton(h->gateway, &r.gateway) == 0) {
//    LOG(LOG_ERR, MN, "inet_aton() failed");
    return -1;
  }
  if (write_route(KernelRoutingSocket, &r) < 0) {
//    LOG(LOG_ERR, MN, "write_route() failed");
    return -1;
  }
  return 0;
}


/***************************************************************
 * readHostname(): Read the hostname. 
 *
 * Input:       none
 * Output:      h -- the hostname as given by gethostname() is 
 *              placed in h->hostname. 
 * Return:      0 on success, -1 on failure 
 * Effects:     No side effects 
 **************************************************************/
int readHostname(HostRow * h)
{
  int      rc;

  memset(h->hostname, 0, HOSTNAME_MAX_LEN);
  /* HOSTNAME_MAX_LEN-1 to ensure that 'name' is NULL-terminated */
  rc = gethostname(h->hostname, HOSTNAME_MAX_LEN - 1);
  if (rc < 0) {
//    LOG(LOG_ERR, MN, "gethostname() failed, %s", strerror(errno));
    return -1;
  }
  return 0;
}
/***************************************************************
 * readHostname(): Read the hostname. 
 *
 * Input:       none
 * Output:      h -- the hostname as given by gethostname() is 
 *              placed in h->hostname. 
 * Return:      0 on success, -1 on failure 
 * Effects:     No side effects 
 **************************************************************/
unsigned char getChecksum(char *buf, int size){
		int i;
		unsigned char checksum;
	
		checksum = 0;
		for(i = 0; i < size; i++){
			checksum += buf[i];
		}
		return checksum;
	}

int readSerial(HostRow * h)
{

	struct sockaddr_in sin;
	struct ifreq ifr;
	char	*addr;
	char    MacAddress[MAC_ADDRESS_MAX_LEN];
	unsigned int ccode=0,pcode=0;
	unsigned char checksum;
	char   Serial[MAC_ADDRESS_MAX_LEN];

	memset(h->hostname, 0, HOSTNAME_MAX_LEN);

	if( SerialLoadFlag == 0){
		memset(&ifr, 0, sizeof(ifr));
		strncpy(ifr.ifr_name, SERIALIF, IFNAMSIZ);

		if(openSocket() == -1) {
		  printf("socket open error\n");
		  return;
		}

		if ((ioctl(Skfd, SIOCGIFHWADDR, &ifr)) < 0) {
//		  LOG(LOG_ERR, MN, "ioctl() failed while reading hw address,%s: %s", SERIALIF,strerror(errno));
		  return -1;
		}
		sprintf(MacAddress,"%02x:%02x:%02x:%02x:%02x:%02x",
								 ifr.ifr_addr.sa_data[0],
								 ifr.ifr_addr.sa_data[1],
								 ifr.ifr_addr.sa_data[2],
								 ifr.ifr_addr.sa_data[3],
								 ifr.ifr_addr.sa_data[4],
								 ifr.ifr_addr.sa_data[5]);
		ccode = (ifr.ifr_addr.sa_data[1] << 8) + ifr.ifr_addr.sa_data[2];
		pcode = (ifr.ifr_addr.sa_data[3] << 16) + (ifr.ifr_addr.sa_data[4] << 8) + ifr.ifr_addr.sa_data[5];

		sprintf(Serial,"%05d-%08d-",ccode,pcode);
		checksum = getChecksum(Serial,strlen(Serial));
		sprintf(h->hostname,"%s%02x",Serial,checksum);
		strcpy(SerialLoad,h->hostname);
		SerialLoadFlag = 1;

		closeSocket();
	} else {
		strcpy(h->hostname,SerialLoad);
	}
	return 0;


  
}

/***************************************************************
 * writeHostname(): Set the hostname. 
 *
 * Input:       h -- the given hostname is in h->hostname. 
 * Output:      none
 * Return:      0 on success, -1 on failure 
 * Effects:     Modifies the kernel hostname. 
 **************************************************************/
int writeHostname(HostRow * h)
{
  int      rc;

  rc = sethostname(h->hostname, strlen(h->hostname));
  if (rc < 0) {
//    LOG(LOG_ERR, MN, "sethostname() failed, %s", strerror(errno));
    return -1;
  }
  return 0;
}


/***************************************************************
 * update_kidx(): Utility function called during appInit() 
 * to initialize the kernel index, kidx, in each Interface 
 * in the RTA Interface table.  The kidx field represents 
 * the index of the interface in the kernel. 
 * Thus, once kidx is initialized, given an Interface object we 
 * can get the index of the interface in the kernel's interface
 * table.  This index is used to determine the routes for a 
 * particular interface, for example.  This function is used as 
 * a callback function to read_interfaces(). 
 *
 * Input:        who -- netlink socket 
 *               nlmsghdr -- netlink message 
 *               arg -- not used 
 * Output:       none 
 * Returns:      0 on success, -1 on failure 
 * Effects:      Modifies kidx fields in RTA Interface table
 ***************************************************************/

int update_kidx(struct sockaddr_nl *who, struct nlmsghdr *n, void *arg)
{
  struct ifinfomsg *ifi = NLMSG_DATA(n);
  struct rtattr *tb[IFLA_MAX + 1];
  int      ifindex;
  unsigned short iftype;
  unsigned ifflags;
  int      ifaddrlen;
  char    *ifname;
  Interface *ptr;

  if (n->nlmsg_type != RTM_NEWLINK)
    return 0;

  if (n->nlmsg_len < NLMSG_LENGTH(sizeof(ifi)))
    return -1;

  memset(tb, 0, sizeof(tb));
  parse_rtattr(tb, IFLA_MAX, IFLA_RTA(ifi), IFLA_PAYLOAD(n));
  if (tb[IFLA_IFNAME] == NULL)
    return 0;

  ifindex = ifi->ifi_index;
  iftype = ifi->ifi_type;
  ifflags = ifi->ifi_flags;

  ifaddrlen = -1;
  if (tb[IFLA_ADDRESS]) {
    ifaddrlen = RTA_PAYLOAD(tb[IFLA_ADDRESS]);
  }
  ifname = RTA_DATA(tb[IFLA_IFNAME]);
  for (ptr = InterfaceHead; ptr; ptr = ptr->next) {
    if (strcmp(ptr->name, ifname) == 0) {
      ptr->kidx = ifindex;
      break;
    }
  }
  return 0;
}

