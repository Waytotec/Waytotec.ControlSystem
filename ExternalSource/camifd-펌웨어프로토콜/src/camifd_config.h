#ifndef _CAMIFD_CONFIG_H
#define _CAMIFD_CONFIG_H

enum {
    CFG_CAMIFDB_PORT = 0
};
enum {
    CFG_IDDB_PORT = 0
};


struct CamifDB;
struct IdDB;

SCODE LoadConfig(void);
SCODE save_camifd_conf(char *, char *);

int cfg_camifdb_get(int type,void *data);
int cfg_iddb_get(int type,void *data);

#endif
