mergeInto(LibraryManager.library, {
  CardzRequestWebAppInstall: function () {
    if (typeof window !== "undefined" && window.CardzPWA && typeof window.CardzPWA.requestInstall === "function") {
      window.CardzPWA.requestInstall();
      return;
    }

    if (typeof window !== "undefined" && typeof window.alert === "function") {
      window.alert("Install is not available yet. On iPhone/iPad, use Share > Add to Home Screen.");
    }
  }
});
