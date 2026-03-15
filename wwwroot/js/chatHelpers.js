window.chatHelpers = {
  scrollToBottom: function (el) {
    try {
      if (!el) return;
      // element reference passed from Blazor is a DOM element
      el.scrollTop = el.scrollHeight;
    } catch (err) {
      console.error("chatHelpers.scrollToBottom error:", err);
    }
  },

  focusElement: function (el) {
    try {
      if (!el) return;
      el.focus();
    } catch (err) {
      console.error("chatHelpers.focusElement error:", err);
    }
  }
};