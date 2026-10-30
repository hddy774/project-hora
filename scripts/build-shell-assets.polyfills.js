/* Offline shims for the Android API 24 stock WebView (Chromium 51).
   Keep this file ES5. Build verification parses it separately as ES5. */
(function (root) {
  'use strict';
  function method(object, name, value) {
    if (!object[name]) Object.defineProperty(object, name, {value: value, configurable: true, writable: true});
  }
  if (typeof root.globalThis === 'undefined') root.globalThis = root;
  method(Object, 'entries', function (value) {
    if (value == null) throw new TypeError('An object is required.');
    var object = Object(value);
    return Object.keys(object).map(function (key) { return [key, object[key]]; });
  });
  method(Object, 'values', function (value) {
    if (value == null) throw new TypeError('An object is required.');
    var object = Object(value);
    return Object.keys(object).map(function (key) { return object[key]; });
  });
  method(Object, 'fromEntries', function (entries) {
    var result = {}, iterator, step, i, entry;
    function add(pair) {
      if (pair == null || (typeof pair !== 'object' && typeof pair !== 'function')) throw new TypeError('An entry is required.');
      Object.defineProperty(result, pair[0], {value: pair[1], enumerable: true, configurable: true, writable: true});
    }
    if (entries == null) throw new TypeError('Entries are required.');
    if (typeof Symbol !== 'undefined' && entries[Symbol.iterator]) {
      iterator = entries[Symbol.iterator]();
      while (!(step = iterator.next()).done) add(step.value);
    } else {
      for (i = 0; i < entries.length; i++) { entry = entries[i]; add(entry); }
    }
    return result;
  });
  method(Object, 'getOwnPropertyDescriptors', function (value) {
    if (value == null) throw new TypeError('An object is required.');
    var object = Object(value), keys = Object.getOwnPropertyNames(object), result = {}, i, key;
    if (Object.getOwnPropertySymbols) keys = keys.concat(Object.getOwnPropertySymbols(object));
    for (i = 0; i < keys.length; i++) {
      key = keys[i];
      Object.defineProperty(result, key, {value: Object.getOwnPropertyDescriptor(object, key), enumerable: true, configurable: true, writable: true});
    }
    return result;
  });
  method(Array.prototype, 'at', function (index) {
    if (this == null) throw new TypeError('An array is required.');
    var object = Object(this), length = object.length >>> 0, offset = Number(index) || 0;
    offset = offset < 0 ? Math.ceil(offset) : Math.floor(offset);
    if (offset < 0) offset += length;
    return offset >= 0 && offset < length ? object[offset] : undefined;
  });
  function replaceChildren() {
    var i, value;
    while (this.firstChild) this.removeChild(this.firstChild);
    for (i = 0; i < arguments.length; i++) {
      value = arguments[i];
      this.appendChild(value instanceof Node ? value : this.ownerDocument.createTextNode(String(value)));
    }
  }
  if (root.Element) method(Element.prototype, 'replaceChildren', replaceChildren);
  if (root.DocumentFragment) method(DocumentFragment.prototype, 'replaceChildren', replaceChildren);
  if (root.Node && !('isConnected' in Node.prototype)) {
    Object.defineProperty(Node.prototype, 'isConnected', {configurable: true, get: function () {
      var node = this;
      while (node) { if (node.nodeType === 9) return true; node = node.parentNode || node.host; }
      return false;
    }});
  }
  if (!root.Intl) {
    Number.prototype.toLocaleString = function () {
      var parts = String(this.valueOf()).split('.');
      parts[0] = parts[0].replace(/\B(?=(\d{3})+(?!\d))/g, ',');
      return parts.join('.');
    };
  }
  function validity() {
    if (!this.checkValidity || this.checkValidity()) return true;
    var invalid = this.querySelector ? this.querySelector('input:invalid,select:invalid,textarea:invalid') : null;
    (invalid || this).focus();
    return false;
  }
  if (root.HTMLFormElement) method(HTMLFormElement.prototype, 'reportValidity', validity);
  if (root.HTMLInputElement) method(HTMLInputElement.prototype, 'reportValidity', validity);
  if (!root.CSS || !CSS.supports || !CSS.supports('display', 'grid')) {
    document.documentElement.setAttribute('data-shell-legacy-grid', '');
  }
  var dialogs = document.getElementsByTagName('dialog'), current = null, mask = null, i;
  function focusables(dialog) {
    return Array.prototype.filter.call(dialog.querySelectorAll('button,input,select,textarea,a[href],[tabindex]'), function (node) {
      return !node.disabled && node.tabIndex >= 0 && node.offsetWidth > 0 && node.offsetHeight > 0;
    });
  }
  function installDialog(dialog) {
    if (typeof dialog.showModal === 'function' && typeof dialog.close === 'function') return;
    dialog.setAttribute('data-shell-legacy-dialog', '');
    dialog.setAttribute('role', 'dialog');
    dialog.setAttribute('aria-modal', 'true');
    if (typeof dialog.open !== 'boolean') Object.defineProperty(dialog, 'open', {get: function () { return this.hasAttribute('open'); }});
    dialog.showModal = function () {
      if (this.open) return;
      if (!mask) { mask = document.createElement('div'); mask.className = 'shell-legacy-backdrop'; document.body.appendChild(mask); }
      current = this; mask.hidden = false; this.setAttribute('open', '');
      var first = focusables(this)[0];
      if (first) first.focus();
    };
    dialog.close = function (returnValue) {
      if (!this.open) return;
      this.returnValue = returnValue == null ? '' : String(returnValue);
      this.removeAttribute('open'); current = null;
      if (mask) mask.hidden = true;
      var event = document.createEvent('Event'); event.initEvent('close', false, false); this.dispatchEvent(event);
    };
  }
  for (i = 0; i < dialogs.length; i++) installDialog(dialogs[i]);
  document.addEventListener('keydown', function (event) {
    if (!current || !current.open) return;
    var key = event.key || event.keyCode, nodes, first, last;
    if (key === 'Escape' || key === 'Esc' || key === 27) {
      current.close(); event.preventDefault(); event.stopPropagation(); return;
    }
    if (key !== 'Tab' && key !== 9) return;
    nodes = focusables(current); first = nodes[0]; last = nodes[nodes.length - 1];
    if (!first) { event.preventDefault(); return; }
    if (event.shiftKey && (document.activeElement === first || !current.contains(document.activeElement))) { last.focus(); event.preventDefault(); }
    else if (!event.shiftKey && (document.activeElement === last || !current.contains(document.activeElement))) { first.focus(); event.preventDefault(); }
  }, true);
  document.addEventListener('focusin', function (event) {
    if (current && current.open && !current.contains(event.target)) {
      var first = focusables(current)[0]; if (first) first.focus();
    }
  }, true);
  root.addEventListener('error', function (event) {
    root.seonroLastHostError = String(event.error && (event.error.stack || event.error) || event.message || 'Script failed.');
  });
}(window));
