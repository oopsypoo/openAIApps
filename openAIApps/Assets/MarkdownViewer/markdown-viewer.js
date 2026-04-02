window.markdownViewer = (function () {
    function setHighlightTheme(href) {
        const link = document.getElementById("highlight-theme");
        if (!link) return;

        link.href = href;
    }

    function setContent(html) {
        const root = document.getElementById("markdown-root");
        if (!root) return;

        root.innerHTML = html || "";

        applyHighlighting();
        addCopyButtons();
    }

    function applyHighlighting() {
        if (!window.hljs) return;

        document.querySelectorAll("pre > code").forEach(function (codeBlock) {
            codeBlock.removeAttribute("data-highlighted");
            hljs.highlightElement(codeBlock);
        });
    }

    function addCopyButtons() {
        const codeBlocks = document.querySelectorAll("pre > code");

        codeBlocks.forEach(function (codeBlock) {
            const pre = codeBlock.parentElement;
            if (!pre) return;

            if (pre.parentElement && pre.parentElement.classList.contains("code-block-wrapper")) {
                return;
            }

            const wrapper = document.createElement("div");
            wrapper.className = "code-block-wrapper";

            pre.parentNode.insertBefore(wrapper, pre);
            wrapper.appendChild(pre);

            const button = document.createElement("button");
            button.className = "code-copy-button";
            button.type = "button";
            button.textContent = "Copy";

            button.addEventListener("click", function () {
                const codeText = (codeBlock.innerText || codeBlock.textContent || "").trimEnd();

                if (window.chrome && window.chrome.webview) {
                    window.chrome.webview.postMessage("copy-code:" + codeText);
                }

                button.textContent = "Copied";
                button.classList.add("copied");

                setTimeout(function () {
                    button.textContent = "Copy";
                    button.classList.remove("copied");
                }, 1200);
            });

            wrapper.appendChild(button);
        });
    }

    function refreshHighlighting() {
        const root = document.getElementById("markdown-root");
        if (!root) return;

        root.querySelectorAll("pre > code").forEach(function (codeBlock) {
            codeBlock.removeAttribute("data-highlighted");
            hljs.highlightElement(codeBlock);
        });
    }

    return {
        setContent: setContent,
        setHighlightTheme: setHighlightTheme,
        refreshHighlighting: refreshHighlighting
    };
})();