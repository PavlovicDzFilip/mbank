for (const opener of document.querySelectorAll("[data-dialog-open]")) {
  opener.addEventListener("click", () => {
    const dialog = document.getElementById(opener.dataset.dialogOpen);
    dialog?.showModal();
    const focusTarget = dialog?.querySelector("input, button, textarea, select");
    focusTarget?.focus();
  });
}

for (const closer of document.querySelectorAll("[data-dialog-close]")) {
  closer.addEventListener("click", () => {
    closer.closest("dialog")?.close();
  });
}

for (const dialog of document.querySelectorAll("dialog.app-dialog, dialog.confirm-dialog")) {
  dialog.addEventListener("click", (event) => {
    if (event.target === dialog) {
      dialog.close();
    }
  });
}

for (const dialog of document.querySelectorAll("dialog[data-show-on-load]")) {
  dialog.showModal();
  dialog.querySelector("input, button, textarea, select")?.focus();
}

for (const copyButton of document.querySelectorAll("[data-copy-target]")) {
  copyButton.addEventListener("click", async () => {
    const target = document.getElementById(copyButton.dataset.copyTarget);
    const value = target?.textContent?.trim();
    if (!value) {
      return;
    }

    await navigator.clipboard.writeText(value);
    const original = copyButton.textContent;
    copyButton.textContent = "Copied";
    window.setTimeout(() => {
      copyButton.textContent = original;
    }, 1400);
  });
}
