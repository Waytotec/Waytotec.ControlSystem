#ifndef _LOGPRT_H
#define _LOGPRT_H
#include <stdarg.h>
#include <syslog.h>

#define __DEBUG
enum {
    TYP_SYSLOG = 0,
    TYP_STDERR
};
/*
#define LOG_EMERG        0    
#define LOG_ALERT           1      
#define LOG_CRIT             2  
#define LOG_ERR              3    
#define LOG_WARNING     4    
#define LOG_NOTICE       5     
#define LOG_INFO             6     
#define LOG_DEBUG            7      
*/
#define LOC __FILE__,__LINE__

extern int logprt_trace;
#ifdef __DEBUG
#define DBG(fmt, args...) if( logprt_trace ) fprintf(stderr, fmt, ## args)
#else
#define DBG(fmt, args...)
#endif

void set_log_type(int type);
void set_log_level(int level);
void logprt_set_trace(void);
void logprt(int level, char *format, ...);

#endif
