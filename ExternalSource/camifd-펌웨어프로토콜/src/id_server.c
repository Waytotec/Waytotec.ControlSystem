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
#include "id_server.h"
#include "id_client.h"
#include "server.h"
#include "logprt.h"

#define offsetof(TYPE, MEMBER) ((size_t) &((TYPE *)0)->MEMBER)

#define container_of(ptr, type, member) ({                  \
    const typeof( ((type *)0)->member ) *__mptr = (ptr);    \
    (type *)( (char *)__mptr - offsetof(type,member) );})

struct id_client_list_entry
{
    struct id_client *id_client;
    LIST_ENTRY(id_client_list_entry) link;
};
    
struct id_server
{
    struct uloop_fd sock_u_fd;
    int sock_fd;
    int id_client_count;
    LIST_HEAD(id_client_list, id_client_list_entry) id_client_list;
    int port;
 
    struct server *server;
};
int id_server_get_port(struct id_server *id_server)
{
    return id_server->port;
}
void id_server_get_id(struct id_server *id_server, char *model, char *sn, char *mac, char *submodel, char *version)
{
    server_get_id(id_server->server,model,sn,mac,submodel,version);
}

static void id_sock_io_cb(struct uloop_fd *u_fd)
{
    struct id_server *id_server = container_of(u_fd, struct id_server, sock_u_fd);
    struct sockaddr_in id_client_addr;
    socklen_t id_client_len = sizeof(id_client_addr);
    int id_client_fd;

    id_client_fd = accept(u_fd->fd, (struct sockaddr *)&id_client_addr, &id_client_len);
    id_client_create(id_server, id_client_fd);
    logprt(LOG_DEBUG,"id_sock accept");
}
void id_server_add_id_client(struct id_server *id_server, struct id_client *id_client)
{
    struct id_client_list_entry *entry;

    entry = malloc(sizeof(*entry));
    entry->id_client = id_client;
    LIST_INSERT_HEAD(&id_server->id_client_list, entry, link);
    id_server->id_client_count++;
}

void id_server_delete_id_client(struct id_server *id_server, struct id_client *id_client)
{
    struct id_client_list_entry *entry;
    struct id_client_list_entry *temp;

    LIST_FOREACH_SAFE(entry, &id_server->id_client_list, link, temp) {
        if (entry->id_client == id_client) {
            LIST_REMOVE(entry, link);
            free(entry);
            id_server->id_client_count--;
        }
    }
}
void id_server_delete_id_client_all(struct id_server *id_server)
{
    struct id_client_list_entry *entry;
    struct id_client_list_entry *temp;

    entry = LIST_FIRST(&id_server->id_client_list);
    LIST_FOREACH_SAFE(entry, &id_server->id_client_list, link, temp) {
        LIST_REMOVE(entry, link);
        free(entry);
        id_server->id_client_count--;
    }
}

struct id_server *id_server_create(struct server *server, int port)
{
    struct sigaction sa;
    struct id_server *id_server;
    struct sockaddr_in addr;
    int v = 1;

    logprt(LOG_DEBUG,"camifd id_server_create");

    memset(&sa, 0, sizeof(sa));
    sa.sa_handler = SIG_IGN;
    sa.sa_flags = SA_RESTART;
    if (sigaction(SIGPIPE, &sa, NULL) < 0) {
        logprt(LOG_ERR, "error to sigaction");
        return NULL;
    }

    id_server = malloc(sizeof(*id_server));
    memset(id_server, 0, sizeof(*id_server));

    id_server->server = server;
    id_server->id_client_count = 0;
    id_server->port = port;

    id_server->sock_fd = socket(PF_INET, SOCK_STREAM, 0);
    if (id_server->sock_fd < 0) {
        logprt(LOG_ERR, "error to socket");
        free(id_server);
        return NULL;
    }
    setsockopt(id_server->sock_fd, SOL_SOCKET, SO_REUSEADDR, (char *)&v, sizeof(int));

    memset(&addr, 0, sizeof(addr));
    addr.sin_family = AF_INET;
    addr.sin_port = htons(port);
    addr.sin_addr.s_addr = INADDR_ANY;
    if (bind(id_server->sock_fd, (struct sockaddr*) &addr, sizeof(addr)) < 0) {
        logprt(LOG_ERR, "error to bind");
        close(id_server->sock_fd);
        free(id_server);
        return NULL;
    }

    if (fcntl(id_server->sock_fd, F_SETFL, fcntl(id_server->sock_fd, F_GETFL, 0) | O_NONBLOCK) < 0) {
        logprt(LOG_ERR, "error to fcntl");
        close(id_server->sock_fd);
        free(id_server);
        return NULL;
    }

    if (listen(id_server->sock_fd, 3) < 0) {
        logprt(LOG_ERR, "error to listen");
        close(id_server->sock_fd);
        free(id_server);
        return NULL;
    }

    id_server->sock_u_fd.cb = id_sock_io_cb;
    id_server->sock_u_fd.fd = id_server->sock_fd;
    uloop_fd_add(&id_server->sock_u_fd, ULOOP_READ);

    return id_server;
}

void id_server_destroy(struct id_server *id_server)
{
    uloop_fd_delete(&id_server->sock_u_fd);
    id_server_delete_id_client_all(id_server);
    close(id_server->sock_fd);
    free(id_server);
}

