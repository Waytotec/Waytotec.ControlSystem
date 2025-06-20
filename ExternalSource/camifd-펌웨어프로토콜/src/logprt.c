#include <stdio.h>
#include <stdlib.h>
#include <stdarg.h>             /* for va_arg */
#include <string.h>
#include <syslog.h>
#include "logprt.h"

static int log_level = LOG_NOTICE;
static int log_type = TYP_SYSLOG;
int logprt_trace=0;
void logprt_set_trace(void)
{
    logprt_trace = 1;
}
void set_log_type(int type)
{
    log_type = type;
}
void set_log_level(int level)
{
    log_level = level;
}

void logprt(int level, char *format, ...)
{                               
  va_list  ap;
  char buf[1024];


  /* Get the optional parameters if any */
  va_start(ap, format);
  vsprintf(buf,format,ap);
  va_end(ap);

  /* Send to syslog() if so configured */
  if( log_type == TYP_SYSLOG ){
    if (level <= log_level)
        syslog(level, "%s", buf);
    }
  /* Send to stderr if so configured */
  if( log_type == TYP_STDERR ){  
      if (level <= log_level ) {
        fprintf(stderr, "%s", buf);
        fprintf(stderr, "\n");
      }
  }
}




