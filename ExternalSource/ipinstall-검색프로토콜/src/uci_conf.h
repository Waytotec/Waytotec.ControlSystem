#ifndef __CONF_H__
#define __CONF_H__

char *conf_get(char *key);
int conf_set(char *key, char *val);
int conf_load(char *name);
int conf_save(char *name);
int conf_init(char *name);
void conf_exit(void);

#endif

