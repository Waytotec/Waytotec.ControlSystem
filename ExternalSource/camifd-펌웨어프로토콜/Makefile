include $(TOPDIR)/rules.mk

PKG_NAME:=camifd
PKG_RELEASE=0

include $(INCLUDE_DIR)/package.mk
include $(INCLUDE_DIR)/cmake.mk

define Package/camifd
  SECTION:=custom
  CATEGORY:=Custom
  DEPENDS:=+libuci
  TITLE:=camifd daemon
endef

define Build/Prepare
	mkdir -p $(PKG_BUILD_DIR)
	$(CP) ./src/* $(PKG_BUILD_DIR)/
endef

define Package/camifd/install
	$(INSTALL_DIR) $(1)/usr/bin
	$(INSTALL_BIN) $(PKG_BUILD_DIR)/camifd $(1)/usr/bin/
	$(INSTALL_DIR) $(1)/etc/config
	$(CP) ./files/camifd.conf $(1)/etc/config/camifd
	$(INSTALL_DIR) $(1)/etc/defaultconf
	$(CP) ./files/camifd.conf $(1)/etc/defaultconf/camifd
	$(INSTALL_DIR) $(1)/etc/init.d
	$(CP) ./files/camifd.init $(1)/etc/init.d/camifd
endef

$(eval $(call BuildPackage,camifd))

