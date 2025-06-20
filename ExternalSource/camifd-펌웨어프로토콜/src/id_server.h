#ifndef _ID_SERVER_H
#define _ID_SERVER_H
struct id_server;
struct id_client;
struct server;

struct id_server *id_server_create(struct server *server,int port);
void id_server_destroy(struct id_server *id_server);
void id_server_add_id_client(struct id_server *id_server, struct id_client *id_client);
void id_server_delete_id_client(struct id_server *id_server, struct id_client *id_client);
void id_server_set_cfg(struct id_server *id_server, int port);
int id_server_get_port(struct id_server *id_server);
void id_server_get_id(struct id_server *id_server, char *model, char *sn, char *mac, char *submodel, char *version);

#endif
