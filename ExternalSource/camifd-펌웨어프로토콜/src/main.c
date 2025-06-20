#include <stdio.h>
#include <unistd.h>
#include <stdlib.h>
#include <string.h>
#include <syslog.h>
#include <sys/stat.h>
#include <sys/types.h>

#include <libubox/uloop.h>

#include "server.h"
#include "typedef.h"
#include "camifd_config.h"
#include "logprt.h"

void usage(void)
{
    fprintf(stderr, "Usage:\n"
        "camifd [-D] [-h] [-l loglevel] [-p pidfile] -e MACaddr -i bdid -s serialnum\n"
        "  Options:\n"
        "    -D                 Run as Daemon\n"
        "    -p pidfile         Write PID to this file\n"
        "    -l loglevel        set loglevel\n"
        "    -e MACaddr         specify the MAC address\n"
        "    -i bdid            specify the board id\n"
        "    -s serialnum       specify the serial number\n"
        "    -h                 This help\n");
}

int create_pidfile(char *pidfile)
{
    FILE *file = fopen(pidfile, "w");

    if (file == NULL) {
        fprintf(stderr, "Can't open %s\n", pidfile);
        return 0;
    } else {
        fprintf(file, "%d\n", getpid());
        fchmod(fileno(file), S_IRUSR | S_IWUSR | S_IRGRP | S_IROTH);
        fclose(file);
    }
    return 1;
}

void remove_pidfile(char *pidfile)
{
    FILE *file = fopen(pidfile, "r");
    int pid;

    if (pidfile == NULL) {
        fprintf(stderr, "Can't open %s\n", pidfile);
    } else {
        fscanf(file, "%d\n", &pid);
        fclose(file);
    if (pid == getpid()) {
            remove(pidfile);
        }
    }
}

void daemonize(void)
{
    pid_t pid, sid;

    if (getppid() == 1) {
    return;
    }

    pid = fork();
    if (pid < 0) {
        exit(EXIT_FAILURE);
    }

    if (pid > 0) {
        exit(EXIT_SUCCESS);
    }

    umask(0);
    sid = setsid();
    if (sid < 0) {
        exit(EXIT_FAILURE);
    }

    if ((chdir("/")) < 0) {
        exit(EXIT_FAILURE);
    }
    freopen( "/dev/null", "r", stdin);
    freopen( "/dev/null", "w", stdout);
    freopen( "/dev/null", "w", stderr);
}

int main(int argc, char* argv[])
{
    struct server *server;
    char *pidfile = NULL;
    char *ethaddr = NULL;
    char *bdid = NULL;
    char *sn = NULL;
    int daemon = 0;
    int c;
    int param[16];
    int signal_flag;
    int log_level = LOG_INFO;

    while ((c = getopt(argc, argv, "Dp:l:h:e:i:s:")) != -1) {
        switch (c) {
        case 'D':
            daemon = 1;
            break;
        case 'p':
            pidfile = strdup(optarg);
            break;
        case 'l':
            log_level = atoi(optarg);
            break;
        case 'e':
            ethaddr = strdup(optarg);
            break;
        case 'i':
            bdid = strdup(optarg);
            break;
        case 's':
            sn = strdup(optarg);
            break;
        case 'h':
        default:
            usage();
            exit(1);
        }
    }
    set_log_level(log_level);
    if (daemon) {
        daemonize();
        set_log_type(TYP_SYSLOG);
    } else {
        set_log_type(TYP_STDERR);
    }

    openlog("[CAMIFD]", LOG_CONS | LOG_PID | LOG_NDELAY, LOG_LOCAL0);
    syslog(LOG_INFO, "Start camifd process with Pid : %d\n", getpid());

    if (pidfile != NULL) {
        if (create_pidfile(pidfile) != 1) {
            logprt(LOG_ERR, "Generate pid file fail !!");
            free(pidfile);
            exit(1);
        }
    }
    while(1){
        uloop_init();
        if( LoadConfig() != S_OK ){
            logprt(LOG_ERR, "LoadConfig Parsing Error!");
            exit(1);

        }

        logprt(LOG_DEBUG,"camifd get config");
        
        if( cfg_camifdb_get(CFG_CAMIFDB_PORT,&param[0]) != 0 ) param[0] = 7061;
        if( cfg_iddb_get(CFG_IDDB_PORT,&param[1]) != 0 ) param[1] = 7000; 

        logprt(LOG_DEBUG,"camifd server_create");
        
        server = server_create(param[0],param[1],ethaddr,bdid,sn);
        printf("camifd server_set_initialized\n");

        server_set_initialized(server);
        printf("camifd uloop_run\n");

        uloop_run();
        signal_flag = server_get_flag(server);
        sleep(1);
        server_destroy(server);

        if(signal_flag != 1) break;

        uloop_done();
    }

    if (pidfile != NULL) {
        remove_pidfile(pidfile);
        free(pidfile);
    }

    syslog(LOG_INFO, "Stop camifd process with Pid : %d\n", getpid());
    closelog();

    return 0;
}
