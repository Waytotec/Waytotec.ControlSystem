#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <fcntl.h>
#include <unistd.h>
#include <signal.h>
#include <syslog.h>
#include <sys/types.h>
#include <sys/wait.h>
#include <sys/un.h>
#include <sys/stat.h>
#include <unistd.h>
#include <errno.h>
#include <time.h>
#include <assert.h>
#include <sys/socket.h>
#include <netinet/in.h>
#include <netinet/ip.h>

#include "typedef.h"
#include "queue.h"
#include "logprt.h"
#include "camifd_config.h"
#include "server.h"
#include "id_server.h"
#include "cam_server.h"

#define offsetof(TYPE, MEMBER) ((size_t) &((TYPE *)0)->MEMBER)

#define container_of(ptr, type, member) ({                  \
    const typeof( ((type *)0)->member ) *__mptr = (ptr);    \
    (type *)( (char *)__mptr - offsetof(type,member) );})

struct server
{
    struct id_server *id_server;
    struct cam_server *cam_server;    
    wtt_eeprom_t eeprom;
    char version[256];
    char submodel[32]; 

    int signal_flag;
    int initialized;
};
struct server *pserver;

static void signal_int_cb(int signo)
{
    uloop_done();
    syslog(LOG_INFO, "camifd exited by interrupt signal!\n");

    exit(0);
}

static void signal_term_cb(int signo)
{
    uloop_done();
    syslog(LOG_INFO, "camifd exited by terminate signal!\n");

    exit(0);
}

static void signal_hup_cb(int signo)
{
    int param[16];

    pserver->signal_flag =0;
    if( LoadConfig() != S_OK ){
        logprt(LOG_ERR, "camifd LoadConfig Parsing Error in SigHub!");
        syslog(LOG_ERR, "camifd LoadConfig Parsing Error!\n");
        exit(1);
    }
        
    if( cfg_camifdb_get(CFG_CAMIFDB_PORT,&param[0]) != 0 ) {logprt(LOG_ERR,"camifdb port get error"); exit(1);}
    if(cfg_iddb_get(CFG_IDDB_PORT,&param[1]) != 0 ) {logprt(LOG_ERR,"iddb port get error"); exit(1);}

//    if( param[0] != server->port || param[1] != server->led.port ){
    if( param[1] != id_server_get_port(pserver->id_server)){
        pserver->signal_flag =1;
        uloop_done();
    } else {
        pserver->signal_flag =0; 
    }
    logprt(LOG_DEBUG,"camifd called sig hup");
}

void server_set_initialized(struct server *server)
{
    server->initialized = 1;
}

int server_get_flag(struct server *server)
{
    return server->signal_flag;
}

void server_get_id(struct server *server, char *model, char *sn, char *mac, char *submodel, char *version)
{
    if( model != NULL) strncpy(model,server->eeprom.model,31);
    if( sn != NULL) strncpy(sn,server->eeprom.sn,31);
    if( mac != NULL) strncpy(mac,server->eeprom.mac,18);
    if( submodel != NULL) strncpy(submodel,server->submodel,31);
    if( version != NULL) strncpy(version,server->version,255);
}

struct server *server_create(int cam_port, int id_port, char *ethaddr, char *bdid, char *sn)
{
    struct sigaction s1, s2, s3, sa;
    struct server *server;
    char *p;
    char line[128], *result;
    FILE *fp;
    char version[128];

    memset(&sa, 0, sizeof(sa));
    sa.sa_handler = SIG_IGN;
    sa.sa_flags = SA_RESTART;
    if (sigaction(SIGPIPE, &sa, NULL) < 0) {
        logprt(LOG_ERR, "error to sigaction");
        return NULL;
    }

    server = malloc(sizeof(*server));
    memset(server, 0, sizeof(*server));
    pserver = server;
    
    p = getenv("ETHADDR");
    if( p == NULL ) strcpy(server->eeprom.mac,ethaddr);
    p = getenv("SN");
    if( p == NULL ) strcpy(server->eeprom.sn,sn);
    p = getenv("BDID");
    if( p == NULL ) strcpy(server->eeprom.model,bdid);

    memset(version,0,sizeof(version));
    fp = fopen(FLASHFILE,"r");
    if(fp){    
        while (fgets(line, sizeof(line), fp) != NULL) {
            if (strncmp(line, "DISTRIB_REVISION", 16) == 0) {
                result = strtok(line, "'");
                if (result != NULL) {
                    result = strtok(NULL, "'");
                    strcpy(version, result);
                }
                
            }
        }
        fclose(fp);
    }
//    p = version + strlen("Firmware Version:  ");
//    strcpy(server->version,p);
    
    strcpy(server->version,version);

    if( strlen(server->eeprom.model) > 8 ){
        strcpy(server->submodel,&server->eeprom.model[9]);
    }
    logprt(LOG_INFO,"BDID:[%s], SN:[%s]",server->eeprom.model,server->eeprom.sn);

    server->id_server = id_server_create(server,id_port);
    server->cam_server = cam_server_create(server,cam_port);    

    memset(&s1, 0, sizeof(s1));
    s1.sa_handler = signal_int_cb;
    s1.sa_flags = 0;
    sigaction(SIGINT, &s1, NULL);
    s1.sa_handler = SIG_IGN;
    sigaction(SIGPIPE, &s1, NULL);

    memset(&s2, 0, sizeof(s2));
    s2.sa_handler = signal_term_cb;
    s2.sa_flags = 0;
    sigaction(SIGTERM, &s2, NULL);
    s2.sa_handler = SIG_IGN;
    sigaction(SIGPIPE, &s2, NULL);

    memset(&s3, 0, sizeof(s3));
    s3.sa_handler = signal_hup_cb;
    s3.sa_flags = 0;
    sigaction(SIGHUP, &s3, NULL);
    s3.sa_handler = SIG_IGN;
    sigaction(SIGPIPE, &s3, NULL);

    return server;
}

void server_destroy(struct server *server)
{

    id_server_destroy(server->id_server);
    cam_server_destroy(server->cam_server);    
    free(server);
    logprt(LOG_INFO,"camifd destroy done");    
}

