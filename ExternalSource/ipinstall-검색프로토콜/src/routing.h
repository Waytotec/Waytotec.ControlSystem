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

#include <asm/types.h> /* for __u32 */
#include <arpa/inet.h> /* for struct in_addr */
#include <linux/rtnetlink.h> /* for struct rtmsg */

struct rtsock
{

  int      sock;
  __u32    seq;
};


struct route
{
  char     dst_valid;
  char     gateway_valid;
  struct in_addr dst;
  struct in_addr gateway;
  int      oif;
  struct rtmsg rtmsg;
  struct route *next;
};


struct rtsock *open_route_socket();
int      read_routes(struct rtsock *s,
  int (*doit) (struct sockaddr_nl *, struct nlmsghdr * n, void *), void *arg);
int      write_route(struct rtsock *s, struct route *route);

int      filter_static_routes_on_inf(struct route *in_routes,
  struct route **out_routes, int inf_index);

int      write_static_routes(struct rtsock *s, struct route *routes,
  int inf_index);
int      delete_routes(struct route *routes);
int      read_interfaces(struct rtsock *s,
  int (*doit) (struct sockaddr_nl *, struct nlmsghdr * n, void *), void *arg);
int      close_route_socket(struct rtsock *s);
void     list_delete(struct route **list);
int      parse_rtattr(struct rtattr *tb[], int max, struct rtattr *rta,
  int len);
int      route_append(struct sockaddr_nl *who, struct nlmsghdr *n, void *arg);
