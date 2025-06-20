#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/types.h>
#include <sys/wait.h>
#include <string.h>
#include <sys/socket.h>
#include <sys/un.h>
#include <sys/stat.h>
#include <unistd.h>
#include <errno.h>
#include <signal.h>
#include <syslog.h>
#include <time.h>
#include <assert.h>

#include "typedef.h"
#include "camifd_config.h"
#include "logprt.h"

struct CamifDB
{
    int port;
};
struct IdDB
{
    int port;
};


typedef struct CamifDB CamifDB_t;
typedef struct IdDB IdDB_t;

CamifDB_t CamifDBData;
IdDB_t IdDBData;

int cfg_camifdb_get(int type,void *data)
{
    int ret = 0;
    switch(type){
        case CFG_CAMIFDB_PORT : 
            *(int *)data = CamifDBData.port;
            break;
         default : 
            ret = -1;
            break;
    }
    return ret;
                        
}
int cfg_iddb_get(int type,void *data)
{
    int ret = 0;
    switch(type){
        case CFG_IDDB_PORT : 
            *(int *)data = IdDBData.port;
            break;
         default : 
            ret = -1;
            break;
    }
    return ret;
                        
}

static void config_set_long(char *szConfValue, void *pObj)
{
    char *pcEnd;
    *((long *)pObj) = strtol(szConfValue, &pcEnd, 10);
}

static void save_set_long(char *szConfValue, void *pObj)
{
    sprintf(szConfValue,"%d",*(int *)pObj);
}

SCODE LoadConfig(void)
{
    CamifDB_t *pCamifDB = &CamifDBData;
    IdDB_t* pIdDB = &IdDBData;

    conf_init("camifd");
    pCamifDB->port = atoi(conf_get("camifd.camifdb.port"));
    pIdDB->port = atoi(conf_get("camifd.iddb.port"));
    
    return S_OK;
}

SCODE save_camifd_conf(char *camifdb_port, char *iddb_port)
{

    conf_set("camifd.camifdb.port", camifdb_port);
    conf_set("camifd.iddb.port", iddb_port);
    conf_save("camifd");

    logprt(LOG_INFO, "camifd configuartion is changed.\n");

    return S_OK;
 }
