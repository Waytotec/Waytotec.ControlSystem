include $(TOPDIR)/rules.mk

PKG_NAME:=ipinstall
PKG_RELEASE=0

include $(INCLUDE_DIR)/package.mk
include $(INCLUDE_DIR)/cmake.mk

define Package/ipinstall
  SECTION:=utils
  CATEGORY:=Custom
  DEPENDS:=+libuci
  TITLE:=ipinstall 
endef

define Build/Prepare
	mkdir -p $(PKG_BUILD_DIR)
	$(CP) ./src/* $(PKG_BUILD_DIR)/
endef

define Package/ipinstall/install
	$(INSTALL_DIR) $(1)/usr/bin
	$(INSTALL_BIN) $(PKG_BUILD_DIR)/ipinstall $(1)/usr/bin/

	$(INSTALL_DIR) $(1)/etc/init.d
	$(INSTALL_BIN) ./files/ipinstall.init $(1)/etc/init.d/ipinstall

	$(INSTALL_DIR) $(1)/etc
	$(INSTALL_BIN) ./files/log_memory.sh $(1)/etc/log_memory.sh
endef

$(eval $(call BuildPackage,ipinstall))
