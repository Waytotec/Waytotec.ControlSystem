#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <fcntl.h>
#include <unistd.h>
#include <signal.h>
#include <syslog.h>
#include <errno.h>
#include <sys/types.h>
#include <sys/socket.h>
#include <netinet/in.h>
#include <netinet/ip.h>
#include <libubox/uloop.h>
#include <time.h>

#include "queue.h"
#include "id_server.h"
#include "id_client.h"
#include "server.h"
#include "strutil.h"
#include "logprt.h"

#define CLIENT_MAX_BUFFER   8096

#define offsetof(TYPE, MEMBER) ((size_t) &((TYPE *)0)->MEMBER)

#define container_of(ptr, type, member) ({                  \
    const typeof( ((type *)0)->member ) *__mptr = (ptr);    \
    (type *)( (char *)__mptr - offsetof(type,member) );})

struct id_client_list_entry
{
    struct id_client *id_client;
    LIST_ENTRY(id_client_list_entry) link;
};
    
struct id_client
{
    struct uloop_fd sock_u_fd;
    int sock_fd;
    struct id_server *id_server;
    char ibuf[CLIENT_MAX_BUFFER];
    int ibuf_count;
};
void id_client_send(struct id_client *id_client, void *data, long data_size, long sec, long nsec);

////////////////////////////////////////////////////////////////////////////////////////////////////

static void id_process_input(struct id_client *id_client)
{
 
    char *buffer = id_client->ibuf; /* static so zero filled */
    char tmp[128];
    char model[32],sn[32],mac[32],submodel[32],version[256];
    char data[1024];

    logprt(LOG_DEBUG,"id_process_input[%d]: %s",id_client->ibuf_count,buffer);      
    if( get_str_data(buffer,"CMD", tmp) ){
        if(strncmp("GETEEPROM",tmp,9) == 0 ){
            id_server_get_id(id_client->id_server, model,sn,mac,submodel,version);
            memset(data,0,sizeof(data));
            sprintf(data,"MODEL=%s;SN=%s;MAC=%s;SUBMODEL=%s;VERSION=%s\r\n",model,sn,mac,submodel,version);
            id_client_send(id_client,data,strlen(data),1,0);            
            logprt(LOG_DEBUG,"camifd id : recv[%d] : %s",id_client->ibuf_count,buffer);            
        }
    }
    id_client->ibuf_count = 0;

}

////////////////////////////////////////////////////////////////////////////////////////////////////
static void id_sock_read_cb(struct uloop_fd *u_fd)
{
    struct id_client *id_client = container_of(u_fd, struct id_client, sock_u_fd);
    ssize_t count;
    char *prn;

    if (id_client->ibuf_count >= sizeof(id_client->ibuf)) {
        id_client->ibuf_count = 0;
    }

    count = recv(u_fd->fd, &id_client->ibuf[id_client->ibuf_count], CLIENT_MAX_BUFFER, 0);
    logprt(LOG_DEBUG,"sock_read_cb %d",count);
    if (count > 0) {
        id_client->ibuf_count += count;
        prn = strstr(id_client->ibuf,"\r\n");
        if ( prn != NULL ){
            id_process_input(id_client);
        } else {
                id_client->ibuf_count += count;
        }
    } else if (count < 0) {
        if (errno != EINTR && errno != EAGAIN) {
            id_client_destroy(id_client);
        }
    } else {
        id_client_destroy(id_client);
    }
}

void id_client_send(struct id_client *id_client, void *data, long data_size, long sec, long nsec)
{
    struct timespec now;
    struct timespec out;
    struct iovec iov[3];
    struct timeval tv;
    struct msghdr msg;
    fd_set wfds;
    int sent;
    int left;
    int ret;

    sent = 0;
    left = data_size;

    clock_gettime(CLOCK_MONOTONIC, &out);
    out.tv_sec  += sec;
    out.tv_nsec += nsec;
    if (out.tv_nsec > 1000000000L) {
        out.tv_sec  += 1;
        out.tv_nsec -= 1000000000L;
    }

    while (left > 0) {
        FD_ZERO(&wfds);
        FD_SET(id_client->sock_fd, &wfds);

        clock_gettime(CLOCK_MONOTONIC, &now);

        tv.tv_sec  = out.tv_sec  - now.tv_sec;
        tv.tv_usec = out.tv_nsec / 1000 - now.tv_nsec / 1000;
        if (tv.tv_usec < 0) {
                tv.tv_sec  -= 1;
                tv.tv_usec += 1000000;
        }

        if ((tv.tv_sec < 0) || (tv.tv_sec == 0 && tv.tv_usec <= 0)) {
            return;
        }

        ret = select(id_client->sock_fd + 1, NULL, &wfds, NULL, &tv);
        if (ret < 0) {
            if (errno == EINTR) {
                continue;
            } else {
                id_client_destroy(id_client);
                return;
            }
        } else if (ret == 0) {
            return;
        }

        if (FD_ISSET(id_client->sock_fd, &wfds)) {
            if( sent < data_size ){
                msg.msg_name = NULL;
                msg.msg_namelen = 0;
                msg.msg_iov = iov;
                msg.msg_iovlen = 1;
                msg.msg_control = NULL;
                msg.msg_controllen = 0;
                msg.msg_flags = 0;

                iov[0].iov_base = data + sent;
                iov[0].iov_len = data_size - sent;
            }
            ret = sendmsg(id_client->sock_fd, &msg, 0);
            if (ret < 0) {
                    if (errno == EINTR) {
                        continue;
                    } else {
                        id_client_destroy(id_client);
                        return;
                    }
            } else if (ret == 0) {
                    return;
            }

            sent += ret;
            left -= ret;
        }
    }
//    if( left == 0 ) id_client_destroy(id_client);
}

int id_client_create(struct id_server *id_server, int fd)
{
    struct id_client *id_client;

    id_client = malloc(sizeof(*id_client));
    memset(id_client, 0, sizeof(*id_client));

    id_client->sock_fd = fd;
    id_client->id_server = id_server;

    fcntl(id_client->sock_fd, F_SETFL, fcntl(id_client->sock_fd, F_GETFL, 0) | O_NONBLOCK);

    id_client->sock_u_fd.cb = id_sock_read_cb;
    id_client->sock_u_fd.fd = id_client->sock_fd;
    uloop_fd_add(&id_client->sock_u_fd, ULOOP_READ);

    id_server_add_id_client(id_server, id_client);
    logprt(LOG_DEBUG,"id_client create");
    return 0;
}

void id_client_destroy(struct id_client *id_client)
{
    if (id_client->id_server) {
        id_server_delete_id_client(id_client->id_server, id_client);
    }

    uloop_fd_delete(&id_client->sock_u_fd);

    if (id_client->sock_fd) {
        close(id_client->sock_fd);
    }
    free(id_client);
    logprt(LOG_DEBUG,"id_client destroy");    
}

