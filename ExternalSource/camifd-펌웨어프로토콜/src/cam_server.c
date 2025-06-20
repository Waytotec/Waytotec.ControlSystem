#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <fcntl.h>
#include <unistd.h>
#include <signal.h>
#include <syslog.h>
#include <sys/types.h>
#include <sys/socket.h>
#include <netinet/in.h>
#include <netinet/ip.h>
#include <libubox/uloop.h>

#include "queue.h"
#include "cam_server.h"
#include "cam_client.h"
#include "server.h"
#include "logprt.h"

#define offsetof(TYPE, MEMBER) ((size_t) &((TYPE *)0)->MEMBER)

#define container_of(ptr, type, member) ({                  \
    const typeof( ((type *)0)->member ) *__mptr = (ptr);    \
    (type *)( (char *)__mptr - offsetof(type,member) );})

struct cam_client_list_entry
{
    struct cam_client *cam_client;
    LIST_ENTRY(cam_client_list_entry) link;
};
    
struct cam_server
{
    struct uloop_fd sock_u_fd;
    int sock_fd;
    int cam_client_count;
    LIST_HEAD(cam_client_list, cam_client_list_entry) cam_client_list;
    int port;
    int upgrade;
    char loadversion[128];
    struct server *server;
};
int cam_server_get_port(struct cam_server *cam_server)
{
    return cam_server->port;
}
int cam_server_get_upgrade(struct cam_server *cam_server)
{
    return cam_server->upgrade;
}

void cam_server_set_upgrade(struct cam_server *cam_server, int upgrade_state)
{
    cam_server->upgrade = upgrade_state;
}
void cam_server_get_loadversion(struct cam_server *cam_server, char *loadversion)
{
    strncpy(loadversion,cam_server->loadversion,127);
}

void cam_server_set_loadversion(struct cam_server *cam_server, char *loadversion)
{
    strncpy(cam_server->loadversion,loadversion,127);
}
void cam_server_get_id(struct cam_server *cam_server, char *model, char *sn, char *mac, char *submodel, char *version)
{
    server_get_id(cam_server->server,model,sn,mac,submodel,version);
}

void cam_server_upgrade_abort_all(struct cam_server *cam_server)
{
    struct cam_client_list_entry *entry;
    struct cam_client_list_entry *temp;

    LIST_FOREACH_SAFE(entry, &cam_server->cam_client_list, link, temp) {
        cam_client_upgrade_abort(entry->cam_client);
    }
    cam_server->upgrade = 0;
}

static void cam_sock_io_cb(struct uloop_fd *u_fd)
{
    struct cam_server *cam_server = container_of(u_fd, struct cam_server, sock_u_fd);
    struct sockaddr_in cam_client_addr;
    socklen_t cam_client_len = sizeof(cam_client_addr);
    int cam_client_fd;

    cam_client_fd = accept(u_fd->fd, (struct sockaddr *)&cam_client_addr, &cam_client_len);
    cam_client_create(cam_server, cam_client_fd);
    logprt(LOG_DEBUG,"cam_sock accept");
}

void cam_server_add_cam_client(struct cam_server *cam_server, struct cam_client *cam_client)
{
    struct cam_client_list_entry *entry;

    entry = malloc(sizeof(*entry));
    entry->cam_client = cam_client;
    LIST_INSERT_HEAD(&cam_server->cam_client_list, entry, link);
    cam_server->cam_client_count++;
}

void cam_server_delete_cam_client(struct cam_server *cam_server, struct cam_client *cam_client)
{
    struct cam_client_list_entry *entry;
    struct cam_client_list_entry *temp;

    LIST_FOREACH_SAFE(entry, &cam_server->cam_client_list, link, temp) {
        if (entry->cam_client == cam_client) {
            LIST_REMOVE(entry, link);
            free(entry);
            cam_server->cam_client_count--;
        }
    }
}

void cam_server_delete_cam_client_all(struct cam_server *cam_server)
{
    struct cam_client_list_entry *entry;
    struct cam_client_list_entry *temp;

    entry = LIST_FIRST(&cam_server->cam_client_list);
    LIST_FOREACH_SAFE(entry, &cam_server->cam_client_list, link, temp) {
        LIST_REMOVE(entry, link);
        free(entry);
        cam_server->cam_client_count--;
    }
}

struct cam_server *cam_server_create(struct server *server, int port)
{
    struct sigaction sa;
    struct cam_server *cam_server;
    struct sockaddr_in addr;
    int v = 1;
    logprt(LOG_DEBUG,"camifd cam_server_create");

    memset(&sa, 0, sizeof(sa));
    sa.sa_handler = SIG_IGN;
    sa.sa_flags = SA_RESTART;
    if (sigaction(SIGPIPE, &sa, NULL) < 0) {
        logprt(LOG_ERR,  "error to sigaction");
        return NULL;
    }

    cam_server = malloc(sizeof(*cam_server));
    memset(cam_server, 0, sizeof(*cam_server));

    cam_server->server = server;
    cam_server->cam_client_count = 0;
    cam_server->port = port;
    strncpy(cam_server->loadversion,"NONE",127);

    cam_server->sock_fd = socket(PF_INET, SOCK_STREAM, 0);
    if (cam_server->sock_fd < 0) {
        logprt(LOG_ERR,  "error to socket");
        free(cam_server);
        return NULL;
    }
    setsockopt(cam_server->sock_fd, SOL_SOCKET, SO_REUSEADDR, (char *)&v, sizeof(int));

    memset(&addr, 0, sizeof(addr));
    addr.sin_family = AF_INET;
    addr.sin_port = htons(port);
    addr.sin_addr.s_addr = INADDR_ANY;
    if (bind(cam_server->sock_fd, (struct sockaddr*) &addr, sizeof(addr)) < 0) {
        logprt(LOG_ERR,  "error to bind");
        close(cam_server->sock_fd);
        free(cam_server);
        return NULL;
    }

    if (fcntl(cam_server->sock_fd, F_SETFL, fcntl(cam_server->sock_fd, F_GETFL, 0) | O_NONBLOCK) < 0) {
        logprt(LOG_ERR,  "error to fcntl");
        close(cam_server->sock_fd);
        free(cam_server);
        return NULL;
    }

    if (listen(cam_server->sock_fd, 3) < 0) {
        logprt(LOG_ERR,  "error to listen");
        close(cam_server->sock_fd);
        free(cam_server);
        return NULL;
    }

    cam_server->sock_u_fd.cb = cam_sock_io_cb;
    cam_server->sock_u_fd.fd = cam_server->sock_fd;
    uloop_fd_add(&cam_server->sock_u_fd, ULOOP_READ);

    return cam_server;
}

void cam_server_destroy(struct cam_server *cam_server)
{
    uloop_fd_delete(&cam_server->sock_u_fd);
    cam_server_delete_cam_client_all(cam_server);
    close(cam_server->sock_fd);
    free(cam_server);
}

