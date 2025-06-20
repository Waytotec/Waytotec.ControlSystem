#ifndef _CAM_SERVER_H
#define _CAM_SERVER_H
struct cam_server;
struct cam_client;
struct server;

struct cam_server *cam_server_create(struct server *server,int port);
void cam_server_destroy(struct cam_server *cam_server);
void cam_server_add_cam_client(struct cam_server *cam_server, struct cam_client *cam_client);
void cam_server_delete_cam_client(struct cam_server *cam_server, struct cam_client *cam_client);
void cam_server_set_cfg(struct cam_server *cam_server, int port);
int cam_server_get_port(struct cam_server *cam_server);
int cam_server_get_upgrade(struct cam_server *cam_server);
void cam_server_set_upgrade(struct cam_server *cam_server, int upgrade_state);
void cam_server_upgrade_abort_all(struct cam_server *cam_server);
void cam_server_get_loadversion(struct cam_server *cam_server, char *loadversion);
void cam_server_set_loadversion(struct cam_server *cam_server, char *loadversion);
void cam_server_get_id(struct cam_server *cam_server, char *model, char *sn, char *mac, char *submodel, char *version);

#endif
