// Auth0WebPlugin.jslib
var Auth0WebPlugin = {
    OpenAuth0Popup: function (popupUrlPtr, returnObjectNamePtr) {
        var popupUrl = UTF8ToString(popupUrlPtr);
        var returnObjectName = UTF8ToString(returnObjectNamePtr);

        var features = 'width=600,height=700,menubar=no,toolbar=no,resizable=yes,scrollbars=yes';
        var popup = window.open(popupUrl, 'Auth0Popup', features);

        if (!popup || popup.closed || typeof popup.closed === 'undefined') {
            console.error('Auth0 popup blocked or failed to open.');
            return 0;
        }

        // 1) Detect popup closed (no code)
        var interval = setInterval(function () {
            if (popup.closed) {
                clearInterval(interval);
                if (typeof SendMessage === 'function') {
                    SendMessage(returnObjectName, 'OnAuth0PopupClosed');
                }
            }
        }, 750);

        // 2) Listen for success message from the popup (preferred)
        // Your Auth0 callback page should run:
        //   window.opener.postMessage({ type: 'auth0:return', code: '<theCode>' }, '*');
        function onMessage(e) {
            var data = e && e.data;
            if (data && (data.type === 'auth0:return') && typeof data.code === 'string' && data.code.length > 0) {
                try { popup.close(); } catch (err) {}
                clearInterval(interval);
                if (typeof SendMessage === 'function') {
                    SendMessage(returnObjectName, 'OnAuth0ReturnCode', data.code);
                }
                window.removeEventListener('message', onMessage);
            }
        }
        window.addEventListener('message', onMessage);

        return 1;
    }
};

mergeInto(LibraryManager.library, Auth0WebPlugin);
