#ifndef _ID_CLIENT_H
#define _ID_CLIENT_H
struct id_client;
struct id_server;

int id_client_create(struct id_server *id_server, int fd);
void id_client_destroy(struct id_client *id_client);

#endif

