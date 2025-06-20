#ifndef _CAM_CLIENT_H
#define _CAM_CLIENT_H
struct cam_client;
struct cam_server;

int cam_client_create(struct cam_server *cam_server, int fd);
void cam_client_destroy(struct cam_client *cam_client);
int cam_client_get_upgrade(struct cam_client *cam_client);
void cam_client_set_upgrade(struct cam_client *cam_client,int upgrade_state);
void cam_client_upgrade_abort(struct cam_client *cam_client);
void cam_client_upgrade_abort_all(struct cam_client *cam_client);
void cam_client_get_loadversion(struct cam_client *cam_client, char *loadversion);
void cam_client_set_loadversion(struct cam_client *cam_client, char *loadversion);
void cam_client_get_id(struct cam_client *cam_client, char *model, char *sn, char *mac, char *submodel, char *version);

#endif
