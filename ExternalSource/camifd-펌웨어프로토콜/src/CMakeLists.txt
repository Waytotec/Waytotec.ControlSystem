cmake_minimum_required(VERSION 2.6)
project(camifd C)
add_definitions(-g -Wall --std=gnu99 -Wextra -Wmissing-declarations -Wuninitialized -Wmaybe-uninitialized)
set(CMAKE_SHARED_LIBRARY_LINK_C_FLAGS "")

set(SOURCES cam_client.c cam_server.c id_client.c logprt.c server.c uci_conf.c cam_proto.c camifd_config.c id_server.c main.c strutil.c)

set(LIBS uci ubox)
add_executable(camifd ${SOURCES})
target_link_libraries(camifd ${LIBS})
install(TARGETS camifd RUNTIME DESTINATION /usr/bin)

