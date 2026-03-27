window.chatHelpers = {
  scrollToBottom: function (el) {
    try {
      if (!el) return;
      // if an element reference wrapper is passed, unwrap DOM node
      const node = el instanceof HTMLElement ? el : el;
      node.scrollTop = node.scrollHeight;
    } catch (e) {
      console && console.debug && console.debug("chatHelpers.scrollToBottom failed", e);
    }
  },

  focusElement: function (el) {
    try {
      if (!el) return;
      const node = el instanceof HTMLElement ? el : el;
      node.focus();
    } catch (e) {
      console && console.debug && console.debug("chatHelpers.focusElement failed", e);
    }
  }
};