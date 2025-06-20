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
#include "cam_server.h"
#include "cam_client.h"
#include "cam_proto.h"
#include "logprt.h"

#define CLIENT_MAX_BUFFER   8096

enum {
    RCV_HDRSIZE = 0,
    RCV_HDR,
    RCV_DATA
}; // packet receive state

#define offsetof(TYPE, MEMBER) ((size_t) &((TYPE *)0)->MEMBER)

#define container_of(ptr, type, member) ({                  \
    const typeof( ((type *)0)->member ) *__mptr = (ptr);    \
    (type *)( (char *)__mptr - offsetof(type,member) );})

struct cam_client_list_entry
{
    struct cam_client *cam_client;
    LIST_ENTRY(cam_client_list_entry) link;
};
    
struct cam_client
{
    struct uloop_fd sock_u_fd;
    int sock_fd;
    struct cam_server *cam_server;
    char ibuf[CLIENT_MAX_BUFFER];
    int ibuf_count;
    int rcv_state;
    int rcv_left;
    int rcv_len;
    pkt_t sndpkt;
    pkt_t rcvpkt;
    upgrade_t up;
    int authlevel;
};
void cam_client_send(struct cam_client *cam_client, void *data, long data_size, long sec, long nsec);

void cam_send_packet(struct cam_client *cam_client)
{
    cam_client_send(cam_client,&cam_client->rcvpkt.phdr, cam_client->rcvpkt.cmdhdrsize,1,0);
    cam_client_send(cam_client,&cam_client->rcvpkt.data, cam_client->rcvpkt.totalsize,1,0);

}

////////////////////////////////////////////////////////////////////////////////////////////////////
#if 0
static void cam_process_input(struct cam_client *cam_client)
{
 
    long ret;
    char *buffer = cam_client->ibuf; /* static so zero filled */
    proto_t *proto = (proto_t *)cam_client->ibuf;

    logprt(LOG_DEBUG,"cam_process_input : %s[%d]",buffer,cam_client->ibuf_count);
    printf("%d,%d,%02x\n",cam_client->rcvpkt.cmdhdrsize,cam_client->rcvpkt.totalsize,proto->checksum);
    printf("checksum : %02x, %s\n",get_checksum(cam_client->rcvpkt.data,cam_client->rcvpkt.totalsize),cam_client->rcvpkt.data);
    ret = cam_client->ibuf_count;
  
    cam_client->ibuf_count = 0;

}
#endif

////////////////////////////////////////////////////////////////////////////////////////////////////
int cam_client_get_upgrade(struct cam_client *cam_client)
{
    return cam_server_get_upgrade(cam_client->cam_server);
}
void cam_client_set_upgrade(struct cam_client *cam_client,int upgrade_state)
{
    cam_server_set_upgrade(cam_client->cam_server,upgrade_state);
}
void cam_client_get_loadversion(struct cam_client *cam_client, char *loadversion){
    cam_server_get_loadversion(cam_client->cam_server, loadversion);
}
void cam_client_set_loadversion(struct cam_client *cam_client, char *loadversion){
    cam_server_set_loadversion(cam_client->cam_server, loadversion);
}
void cam_client_upgrade_abort(struct cam_client *cam_client)
{
    if( cam_client->up.update != 0 ){ // UPDATE_IDLE
        cam_client_destroy(cam_client);
    }
}
void cam_client_upgrade_abort_all(struct cam_client *cam_client)
{
    cam_server_upgrade_abort_all(cam_client->cam_server);
}
void cam_client_get_id(struct cam_client *cam_client, char *model, char *sn, char *mac, char *submodel, char *version){
    cam_server_get_id(cam_client->cam_server,model,sn,mac,submodel,version);
}

static void cam_sock_read_cb(struct uloop_fd *u_fd)
{
    struct cam_client *cam_client = container_of(u_fd, struct cam_client, sock_u_fd);
    ssize_t count;
    char tmp[32];
    proto_t *proto;
    unsigned char checksum = 0x0;
    int ret;

    if (cam_client->ibuf_count >= sizeof(cam_client->ibuf)) {
        cam_client->ibuf_count = 0;
    }

    count = recv(u_fd->fd, &cam_client->ibuf[cam_client->ibuf_count], cam_client->rcv_left, 0);
    logprt(LOG_DEBUG,"sock_read_cb %d",count);
    if (count > 0) {
        cam_client->ibuf_count += count;
        switch(cam_client->rcv_state){
            case RCV_HDRSIZE :
                cam_client->rcv_len += count;
                cam_client->rcv_left -= count;
                if( cam_client->rcv_left <= 0 ){
                    memset(tmp,0,sizeof(tmp));
                    strncpy(tmp,cam_client->ibuf,2);
                    cam_client->rcvpkt.cmdhdrsize = atoi(tmp);
                    cam_client->rcv_state = RCV_HDR;
                    cam_client->rcv_left = cam_client->rcvpkt.cmdhdrsize;
                    cam_client->rcv_len = 0;
                }
                break;
            case RCV_HDR :
                cam_client->rcv_len += count;
                cam_client->rcv_left -= count;
                if( cam_client->rcv_left <= 0 ){
                    memset(tmp,0,sizeof(tmp));
                    strncpy(tmp,cam_client->ibuf+2,12);
                    cam_client->rcvpkt.totalsize = atoi(tmp);
                    memcpy((char *)&cam_client->rcvpkt.phdr,cam_client->ibuf,cam_client->rcvpkt.cmdhdrsize+2);
                    cam_client->rcvpkt.phdr.cmdstr[cam_client->rcvpkt.cmdhdrsize - 14] = 0;
                    cam_client->rcv_state = RCV_DATA;
                    cam_client->rcv_left = cam_client->rcvpkt.totalsize;
                    cam_client->rcv_len = 0;
                }
                break;
            case RCV_DATA :
                cam_client->rcv_len += count;
                cam_client->rcv_left -= count;
                if( cam_client->rcv_left <= 0 ){
                    proto = (proto_t *)cam_client->ibuf;
                    memcpy(cam_client->rcvpkt.data,cam_client->ibuf+cam_client->rcvpkt.cmdhdrsize+2,cam_client->rcvpkt.totalsize);
                    checksum = get_checksum(cam_client->rcvpkt.data,cam_client->rcvpkt.totalsize);
                    if(checksum != proto->checksum){
                        logprt(LOG_INFO,"checksum error : %x %x", checksum, proto->checksum);
                        cam_client->rcvpkt.error_flag = ERR_CHECKSUM;
                    } else {
                        logprt(LOG_DEBUG,"checksum ok : %x %x", checksum, proto->checksum);
                    }
//                    printf(" err : %d,cmdstr : %s\n",cam_client->rcvpkt.error_flag,cam_client->rcvpkt.phdr.cmdstr);
                    if( cam_client->rcvpkt.error_flag == ERR_NOERROR ){
                        if(!strcmp(cam_client->rcvpkt.phdr.cmdstr, CAM_REBOOT)){
                            ret = cam_reboot(cam_client,&cam_client->rcvpkt,cam_client->rcvpkt.phdr.cmdstr);
                            cam_send_packet(cam_client);
                        } else if(!strcmp(cam_client->rcvpkt.phdr.cmdstr, CAM_HARDDEFAULT)){
                            ret = cam_harddefault(cam_client,&cam_client->rcvpkt,cam_client->rcvpkt.phdr.cmdstr);
                            cam_send_packet(cam_client);
                        } else if(!strcmp(cam_client->rcvpkt.phdr.cmdstr, STR_FILEDOWNLOAD)){
                            ret = cam_filedownload(cam_client,&cam_client->rcvpkt,&cam_client->up,cam_client->rcvpkt.phdr.cmdstr);
                            if( ret != 0 ){
                                cam_client_set_upgrade(cam_client,UPDATE_IDLE);
                                memset(cam_client->up.filename,0,sizeof(cam_client->up.filename));
                                cam_client->up.filesize = 0;
                                cam_client->up.update = 0;
                            }
                            cam_send_packet(cam_client);                            
                        } else if(!strcmp(cam_client->rcvpkt.phdr.cmdstr, STR_DOWNDATA)){
                            ret = cam_downdata(cam_client,&cam_client->rcvpkt,&cam_client->up,cam_client->rcvpkt.phdr.cmdstr);
                            if( ret != 0 ){
                                cam_client_set_upgrade(cam_client,UPDATE_IDLE);
                                memset(cam_client->up.filename,0,sizeof(cam_client->up.filename));
                                cam_client->up.filesize = 0;
                                cam_client->up.update = 0;
                            }
                            // no response
                        } else if(!strcmp(cam_client->rcvpkt.phdr.cmdstr, STR_UPGRADE)){
                            ret = cam_upgrade(cam_client,&cam_client->rcvpkt,&cam_client->up,cam_client->rcvpkt.phdr.cmdstr);
                            cam_client_set_upgrade(cam_client,UPDATE_IDLE);
                            cam_send_packet(cam_client);
                            if( ret == 0 ){
                                logprt(LOG_INFO,"%s upgrade end!",cam_client->up.filename);
                            }

                            memset(cam_client->up.filename,0,sizeof(cam_client->up.filename));
                            cam_client->up.filesize = 0;
                            cam_client->up.update = 0;

                        } else if(!strcmp(cam_client->rcvpkt.phdr.cmdstr, STR_UPABORT)){
                            ret = cam_upabort(cam_client,  &cam_client->rcvpkt,cam_client->rcvpkt.phdr.cmdstr);
                            cam_send_packet(cam_client);
                        } else if(!strcmp(cam_client->rcvpkt.phdr.cmdstr, STR_CAMVERSION)){
                            ret = cam_camversion(cam_client,&cam_client->rcvpkt,&cam_client->up,cam_client->rcvpkt.phdr.cmdstr);
                            cam_send_packet(cam_client);
                        }                        
                    } else {
                        if(cam_client->rcvpkt.error_flag == ERR_CHECKSUM){
                            mk_response_msg(&cam_client->sndpkt, cam_client->sndpkt.phdr.cmdstr, cam_client->rcvpkt.error_flag, "CHECKSUM ERROR");
                        } else if(cam_client->rcvpkt.error_flag == ERR_NOT_PROMISE){
                            mk_response_msg(&cam_client->sndpkt, cam_client->sndpkt.phdr.cmdstr, cam_client->rcvpkt.error_flag, "NOT PROMISE");
                        } else if(cam_client->rcvpkt.error_flag == ERR_RECV_TOTALSIZE){
                            mk_response_msg(&cam_client->sndpkt, cam_client->sndpkt.phdr.cmdstr, cam_client->rcvpkt.error_flag, "HEADER LENGTH ERROR");
                        } else if(cam_client->rcvpkt.error_flag == ERR_RECV_TOTALSIZE){
                            mk_response_msg(&cam_client->sndpkt, cam_client->sndpkt.phdr.cmdstr, cam_client->rcvpkt.error_flag, "DATA LENGTH ERROR");
                        } else{
                            mk_response_msg(&cam_client->sndpkt, cam_client->sndpkt.phdr.cmdstr, cam_client->rcvpkt.error_flag, "NOT PROMISE");
                        }
                        cam_send_packet(cam_client);
                    }
                    
                    cam_client->rcv_state = RCV_HDRSIZE;
                    cam_client->rcv_left = 2;
                    cam_client->rcv_len = 0;
                    cam_client->ibuf_count = 0;
                }

                break;            
        }
    } else if (count < 0) {
        if (errno != EINTR && errno != EAGAIN) {
            cam_client_destroy(cam_client);
        }
    } else {
        cam_client_destroy(cam_client);
    }
}

void cam_client_send(struct cam_client *cam_client, void *data, long data_size, long sec, long nsec)
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
        FD_SET(cam_client->sock_fd, &wfds);

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

        ret = select(cam_client->sock_fd + 1, NULL, &wfds, NULL, &tv);
        if (ret < 0) {
            if (errno == EINTR) {
                continue;
            } else {
                cam_client_destroy(cam_client);
                return;
            }
        } else if (ret == 0) {
            return;
        }

        if (FD_ISSET(cam_client->sock_fd, &wfds)) {
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
            ret = sendmsg(cam_client->sock_fd, &msg, 0);
            if (ret < 0) {
                if (errno == EINTR) {
                    continue;
                } else {
                    cam_client_destroy(cam_client);
                    return;
                }
            } else if (ret == 0) {
                return;
            }

            sent += ret;
            left -= ret;
        }
    }
 //   if( left == 0 ) cam_client_destroy(cam_client);
}

int cam_client_create(struct cam_server *cam_server, int fd)
{
    struct cam_client *cam_client;

    cam_client = malloc(sizeof(*cam_client));
    memset(cam_client, 0, sizeof(*cam_client));

    cam_client->sock_fd = fd;
    cam_client->cam_server = cam_server;
    cam_client->rcv_left = 2;
    cam_client->rcv_state = RCV_HDRSIZE;

    fcntl(cam_client->sock_fd, F_SETFL, fcntl(cam_client->sock_fd, F_GETFL, 0) | O_NONBLOCK);

    cam_client->sock_u_fd.cb = cam_sock_read_cb;
    cam_client->sock_u_fd.fd = cam_client->sock_fd;
    uloop_fd_add(&cam_client->sock_u_fd, ULOOP_READ);

    cam_server_add_cam_client(cam_server, cam_client);
    logprt(LOG_DEBUG,"cam_client create");
    return 0;
}

void cam_client_destroy(struct cam_client *cam_client)
{
    if (cam_client->cam_server) {
        cam_server_delete_cam_client(cam_client->cam_server, cam_client);
    }

    uloop_fd_delete(&cam_client->sock_u_fd);

    if (cam_client->sock_fd) {
        close(cam_client->sock_fd);
    }
    free(cam_client);
    logprt(LOG_DEBUG,"cam_client destroy");    
}

