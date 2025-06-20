cmake_minimum_required(VERSION 2.6)

PROJECT(ipinstall C)
ADD_DEFINITIONS(-Os -Wall --std=gnu99 -Wmissing-declarations)

SET(CMAKE_SHARED_LIBRARY_LINK_C_FLAGS "")

IF(APPLE)
  INCLUDE_DIRECTORIES(/opt/local/include)
  LINK_DIRECTORIES(/opt/local/lib)
ENDIF()

SET(SOURCES ipinstall.c network_util.c routing.c uci_conf.c)

#find_library(json NAMES json-c json)
SET(LIBS uci)

IF(DEBUG)
  ADD_DEFINITIONS(-DDEBUG -g3)
  IF(NO_OPTIMIZE)
    ADD_DEFINITIONS(-O0)
  ENDIF()
ENDIF()

ADD_EXECUTABLE(ipinstall ${SOURCES})

TARGET_LINK_LIBRARIES(ipinstall ${LIBS})

INSTALL(TARGETS ipinstall RUNTIME DESTINATION /usr/bin)
