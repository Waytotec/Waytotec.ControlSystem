#ifndef _SERVER_H
#define _SERVER_H

#define FLASHFILE "/etc/openwrt_release"

struct server;

typedef struct _wtt_eeprom_t {
    char mac[32];
    char sn[32];
    char model[32];
} wtt_eeprom_t;

struct server *server_create(int cam_port, int id_port, char *, char *, char *);
void server_destroy(struct server *server);

void server_set_initialized(struct server *server);
int server_get_flag(struct server *server);
void server_get_id(struct server *server, char *model, char *sn, char *mac, char *submodel, char *version);

#endif
