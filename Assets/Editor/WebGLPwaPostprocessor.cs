using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace LastFreeCity.Editor
{
    public static class WebGLPwaPostprocessor
    {
        private const string ManifestFileName = "manifest.webmanifest";
        private const string ServiceWorkerFileName = "sw.js";
        private const string LegacyServiceWorkerFileName = "service-worker.js";
        private const string WebConfigFileName = "web.config";
        private const string Icon192FileName = "pwa-icon-192.svg";
        private const string Icon512FileName = "pwa-icon-512.svg";
        private const string LandingLogoFileName = "landing-logo.png";

        private const string LoaderFileName = "Web.loader.js";
        private const string DataFileName = "Web.data";
        private const string FrameworkFileName = "Web.framework.js";
        private const string WasmFileName = "Web.wasm";

        private const string TesterPassword = "PlaysBadScribble123";
        private const string AdminPassword = "PlaysBadAdmin321";
        private const string TesterAccessKey = "scribblewars-prealpha-access";
        private const string TesterAccessToken = "granted::PlaysBadScribble123";
        private const string AdminAccessKey = "scribblewars-admin-access";
        private const string AdminAccessToken = "granted::PlaysBadAdmin321";
        private const string PublicAccessKey = "scribblewars-public-access";
        private const string LandingLogoSourceRelativePath = @"Assets/UI/Sprites/new/new/logo/logo_variation_4.png";

        private readonly struct VersionedRuntimeFiles
        {
            public readonly string LoaderFileName;
            public readonly string DataFileName;
            public readonly string FrameworkFileName;
            public readonly string WasmFileName;

            public VersionedRuntimeFiles(string loaderFileName, string dataFileName, string frameworkFileName, string wasmFileName)
            {
                LoaderFileName = loaderFileName;
                DataFileName = dataFileName;
                FrameworkFileName = frameworkFileName;
                WasmFileName = wasmFileName;
            }
        }

        [PostProcessBuild(1000)]
        public static void OnPostProcessBuild(BuildTarget target, string buildPath)
        {
            if (target != BuildTarget.WebGL || string.IsNullOrWhiteSpace(buildPath) || !Directory.Exists(buildPath))
            {
                return;
            }

            string indexPath = Path.Combine(buildPath, "index.html");
            if (!File.Exists(indexPath))
            {
                Debug.LogWarning("[Cardz][PWA] Skipped WebGL post-process because index.html was not found.");
                return;
            }

            string buildVersion = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            VersionedRuntimeFiles runtimeFiles = VersionRuntimeFiles(buildPath, buildVersion);

            WriteLandingPage(buildPath, buildVersion);
            WriteGamePage(buildPath, runtimeFiles, buildVersion);
            WriteManifest(buildPath);
            WriteServiceWorker(buildPath, buildVersion);
            DeleteLegacyServiceWorker(buildPath);
            WriteWebConfig(buildPath);
            WriteIcon(buildPath, Icon192FileName);
            WriteIcon(buildPath, Icon512FileName);
            WriteLandingLogo(buildPath);

            Debug.Log($"[Cardz][PWA] WebGL build patched with landing page and gated game page. Version={buildVersion}");
        }

        private static VersionedRuntimeFiles VersionRuntimeFiles(string buildPath, string buildVersion)
        {
            string buildFolderPath = Path.Combine(buildPath, "Build");
            if (!Directory.Exists(buildFolderPath))
            {
                throw new DirectoryNotFoundException($"[Cardz][PWA] Build folder not found: {buildFolderPath}");
            }

            string versionSuffix = $".{buildVersion}";
            string versionedLoader = InsertVersionSuffix(LoaderFileName, versionSuffix);
            string versionedData = InsertVersionSuffix(DataFileName, versionSuffix);
            string versionedFramework = InsertVersionSuffix(FrameworkFileName, versionSuffix);
            string versionedWasm = InsertVersionSuffix(WasmFileName, versionSuffix);

            RenameRuntimeFile(buildFolderPath, LoaderFileName, versionedLoader);
            RenameRuntimeFile(buildFolderPath, DataFileName, versionedData);
            RenameRuntimeFile(buildFolderPath, FrameworkFileName, versionedFramework);
            RenameRuntimeFile(buildFolderPath, WasmFileName, versionedWasm);

            return new VersionedRuntimeFiles(versionedLoader, versionedData, versionedFramework, versionedWasm);
        }

        private static string InsertVersionSuffix(string fileName, string versionSuffix)
        {
            string extension = Path.GetExtension(fileName);
            string nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            return $"{nameWithoutExtension}{versionSuffix}{extension}";
        }

        private static void RenameRuntimeFile(string buildFolderPath, string originalFileName, string versionedFileName)
        {
            string originalPath = Path.Combine(buildFolderPath, originalFileName);
            string versionedPath = Path.Combine(buildFolderPath, versionedFileName);
            if (!File.Exists(originalPath))
            {
                throw new FileNotFoundException($"[Cardz][PWA] Expected runtime file was not found: {originalPath}");
            }

            if (File.Exists(versionedPath))
            {
                File.Delete(versionedPath);
            }

            File.Move(originalPath, versionedPath);
        }

        private static void WriteLandingPage(string buildPath, string buildVersion)
        {
            string html = @"<!DOCTYPE html>
<html lang=""en-us"">
  <head>
    <meta charset=""utf-8"">
    <meta http-equiv=""Content-Type"" content=""text/html; charset=utf-8"">
    <title>Scribble Wars | Pre-Alpha Access</title>
__PWA_SHELL__
    <style>
      :root {
        color-scheme: light;
        --paper: #f7f0df;
        --paper-strong: #fffaf0;
        --ink: #2e2a24;
        --blue: #7aa7ff;
        --red: #ef8e8e;
        --gold: #efcf62;
        --shadow: rgba(46, 42, 36, 0.18);
      }

      * {
        box-sizing: border-box;
      }

      body {
        margin: 0;
        min-height: 100vh;
        font-family: ""Trebuchet MS"", ""Segoe UI"", sans-serif;
        color: var(--ink);
        background:
          radial-gradient(circle at top left, rgba(122, 167, 255, 0.18), transparent 24%),
          radial-gradient(circle at bottom right, rgba(239, 142, 142, 0.16), transparent 28%),
          linear-gradient(180deg, #fdf7ea 0%, #f2ebdb 100%);
        display: flex;
        align-items: center;
        justify-content: center;
        padding: 28px 18px;
      }

      .panel {
        width: min(100%, 560px);
        background: rgba(255, 250, 240, 0.96);
        border: 2px solid rgba(46, 42, 36, 0.22);
        border-radius: 26px;
        box-shadow: 0 26px 60px var(--shadow);
        padding: 28px 24px 24px;
        position: relative;
        overflow: hidden;
      }

      .panel::before {
        content: """";
        position: absolute;
        inset: 10px;
        border: 1px dashed rgba(46, 42, 36, 0.16);
        border-radius: 20px;
        pointer-events: none;
      }

      .logo-wrap {
        display: flex;
        justify-content: center;
        margin-bottom: 12px;
      }

      .logo-wrap img {
        width: min(100%, 300px);
        height: auto;
        image-rendering: auto;
      }

      .eyebrow {
        text-align: center;
        font-size: 0.82rem;
        font-weight: 800;
        letter-spacing: 0.18em;
        text-transform: uppercase;
        color: #7f6a48;
        margin-bottom: 8px;
      }

      h1 {
        margin: 0 0 10px;
        text-align: center;
        font-size: clamp(2rem, 6vw, 3rem);
        line-height: 1;
      }

      .lead {
        margin: 0 auto 18px;
        max-width: 34rem;
        text-align: center;
        font-size: 1rem;
        line-height: 1.5;
      }

      .status {
        border-radius: 16px;
        padding: 12px 14px;
        font-size: 0.96rem;
        line-height: 1.4;
        margin-bottom: 14px;
        border: 1px solid rgba(46, 42, 36, 0.14);
        background: rgba(122, 167, 255, 0.11);
        display: none;
      }

      .status.show {
        display: block;
      }

      .status.admin {
        background: rgba(239, 207, 98, 0.2);
      }

      .status.error {
        background: rgba(239, 142, 142, 0.16);
        color: #8c3030;
      }

      form {
        display: grid;
        gap: 10px;
        margin-bottom: 14px;
      }

      label {
        font-size: 0.86rem;
        font-weight: 700;
        letter-spacing: 0.04em;
        text-transform: uppercase;
      }

      input[type=""password""] {
        width: 100%;
        border: 2px solid rgba(46, 42, 36, 0.18);
        border-radius: 14px;
        padding: 14px 16px;
        font-size: 1rem;
        background: #fffdf8;
        color: var(--ink);
      }

      input[type=""password""]:focus {
        outline: none;
        border-color: rgba(122, 167, 255, 0.9);
        box-shadow: 0 0 0 4px rgba(122, 167, 255, 0.18);
      }

      button {
        appearance: none;
        border: 0;
        border-radius: 14px;
        padding: 14px 16px;
        font-size: 0.98rem;
        font-weight: 800;
        cursor: pointer;
        transition: transform 0.12s ease, box-shadow 0.12s ease, opacity 0.12s ease;
        box-shadow: 0 8px 18px rgba(46, 42, 36, 0.12);
      }

      button:hover {
        transform: translateY(-1px);
      }

      button:active {
        transform: translateY(1px);
      }

      .primary {
        background: #7aa7ff;
        color: #10254d;
      }

      .secondary {
        background: #f0dfaa;
        color: #554316;
      }

      .ghost {
        background: rgba(46, 42, 36, 0.08);
        color: var(--ink);
      }

      .row {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 10px;
      }

      .divider {
        height: 1px;
        background: rgba(46, 42, 36, 0.12);
        margin: 18px 0 14px;
      }

      .admin-panel {
        display: none;
      }

      .admin-panel.show {
        display: block;
      }

      .toggle {
        display: flex;
        align-items: center;
        gap: 10px;
        padding: 12px 14px;
        border: 1px solid rgba(46, 42, 36, 0.14);
        border-radius: 14px;
        background: rgba(255, 255, 255, 0.75);
        margin-top: 10px;
      }

      .toggle input {
        width: 20px;
        height: 20px;
      }

      .footnote {
        margin-top: 16px;
        text-align: center;
        font-size: 0.84rem;
        color: rgba(46, 42, 36, 0.78);
      }

      @media (max-width: 540px) {
        .panel {
          padding: 24px 18px 20px;
        }

        .row {
          grid-template-columns: 1fr;
        }
      }
    </style>
  </head>
  <body>
    <main class=""panel"">
      <div class=""logo-wrap"">
        <img src=""./landing-logo.png?v=__BUILD_VERSION__"" alt=""Scribble Wars logo"">
      </div>
      <div class=""eyebrow"">Pre-Alpha Testing</div>
      <h1>Scribble Wars</h1>
      <p class=""lead"">This build is only for invited testers. Enter the tester password to reach the current game build, or use admin access to manage local tester access on this browser.</p>

      <div id=""status-box"" class=""status""></div>

      <form id=""tester-form"">
        <label for=""tester-password"">Tester Password</label>
        <input id=""tester-password"" type=""password"" autocomplete=""current-password"" placeholder=""Enter tester password"">
        <button class=""primary"" type=""submit"">Enter Test Build</button>
      </form>

      <div class=""row"">
        <button id=""continue-button"" class=""secondary"" type=""button"" hidden>Continue To Game</button>
        <button id=""clear-button"" class=""ghost"" type=""button"" hidden>Clear Stored Access</button>
      </div>

      <div class=""divider""></div>

      <button id=""admin-toggle"" class=""ghost"" type=""button"">Admin Access</button>

      <section id=""admin-panel"" class=""admin-panel"">
        <form id=""admin-form"">
          <label for=""admin-password"">Admin Password</label>
          <input id=""admin-password"" type=""password"" autocomplete=""current-password"" placeholder=""Enter admin password"">
          <button class=""secondary"" type=""submit"">Unlock Admin</button>
        </form>

        <div id=""admin-tools"" hidden>
          <div class=""toggle"">
            <input id=""public-toggle"" type=""checkbox"">
            <label for=""public-toggle"" style=""margin:0; text-transform:none; letter-spacing:0; font-size:0.96rem; font-weight:700;"">Enable public tester access on this browser</label>
          </div>
          <div class=""row"" style=""margin-top:10px;"">
            <button id=""admin-continue"" class=""primary"" type=""button"">Open Admin Game View</button>
            <button id=""admin-clear"" class=""ghost"" type=""button"">Clear Admin Access</button>
          </div>
        </div>
      </section>

      <p class=""footnote"">Access is stored locally in your browser until you clear it. The actual game build is still protected if someone opens the game page directly.</p>
    </main>

    <script>
      (function () {
        const ACCESS_KEY = '__ACCESS_KEY__';
        const ACCESS_TOKEN = '__ACCESS_TOKEN__';
        const ACCESS_PASSWORD = '__TESTER_PASSWORD__';
        const ADMIN_KEY = '__ADMIN_KEY__';
        const ADMIN_TOKEN = '__ADMIN_TOKEN__';
        const ADMIN_PASSWORD = '__ADMIN_PASSWORD__';
        const PUBLIC_KEY = '__PUBLIC_KEY__';

        function getStoredValue(key) {
          try {
            return sessionStorage.getItem(key) || localStorage.getItem(key);
          } catch (error) {
            return null;
          }
        }

        function setStoredValue(key, value, persistent) {
          try {
            (persistent ? localStorage : sessionStorage).setItem(key, value);
          } catch (error) {
          }
        }

        function removeStoredValue(key) {
          try {
            sessionStorage.removeItem(key);
            localStorage.removeItem(key);
          } catch (error) {
          }
        }

        function hasTesterAccess() {
          return getStoredValue(ACCESS_KEY) === ACCESS_TOKEN;
        }

        function hasAdminAccess() {
          return getStoredValue(ADMIN_KEY) === ADMIN_TOKEN;
        }

        function publicAccessEnabled() {
          return getStoredValue(PUBLIC_KEY) === '1';
        }

        function navigateToGame(adminMode) {
          window.location.href = adminMode ? './game.html?admin=1' : './game.html';
        }

        window.addEventListener('DOMContentLoaded', function () {
          const testerForm = document.getElementById('tester-form');
          const testerPassword = document.getElementById('tester-password');
          const continueButton = document.getElementById('continue-button');
          const clearButton = document.getElementById('clear-button');
          const statusBox = document.getElementById('status-box');
          const adminToggle = document.getElementById('admin-toggle');
          const adminPanel = document.getElementById('admin-panel');
          const adminForm = document.getElementById('admin-form');
          const adminPassword = document.getElementById('admin-password');
          const adminTools = document.getElementById('admin-tools');
          const publicToggle = document.getElementById('public-toggle');
          const adminContinue = document.getElementById('admin-continue');
          const adminClear = document.getElementById('admin-clear');

          function showStatus(message, kind) {
            statusBox.textContent = message;
            statusBox.className = 'status show' + (kind ? ' ' + kind : '');
          }

          function hideStatus() {
            statusBox.textContent = '';
            statusBox.className = 'status';
          }

          function refreshAccessState() {
            const tester = hasTesterAccess();
            const admin = hasAdminAccess();
            const publicAccess = publicAccessEnabled();
            const canEnter = tester || admin || publicAccess;

            continueButton.hidden = !canEnter;
            clearButton.hidden = !canEnter;
            adminTools.hidden = !admin;
            adminPanel.classList.toggle('show', adminPanel.dataset.open === '1');
            publicToggle.checked = publicAccess;

            if (admin) {
              showStatus(publicAccess
                ? 'Admin access unlocked. Public tester access is currently enabled on this browser.'
                : 'Admin access unlocked. Public tester access is currently disabled on this browser.', 'admin');
              continueButton.textContent = 'Continue To Tester View';
            } else if (tester) {
              showStatus('Tester access unlocked on this browser. Continue whenever you are ready.');
              continueButton.textContent = 'Continue To Game';
            } else if (publicAccess) {
              showStatus('Public tester access is enabled on this browser by an admin.');
              continueButton.textContent = 'Continue To Game';
            } else {
              hideStatus();
            }
          }

          testerForm.addEventListener('submit', function (event) {
            event.preventDefault();
            if ((testerPassword.value || '').trim() !== ACCESS_PASSWORD) {
              showStatus('That tester password is not correct.', 'error');
              return;
            }

            setStoredValue(ACCESS_KEY, ACCESS_TOKEN, true);
            testerPassword.value = '';
            refreshAccessState();
          });

          continueButton.addEventListener('click', function () {
            navigateToGame(false);
          });

          clearButton.addEventListener('click', function () {
            removeStoredValue(ACCESS_KEY);
            removeStoredValue(PUBLIC_KEY);
            removeStoredValue(ADMIN_KEY);
            refreshAccessState();
          });

          adminToggle.addEventListener('click', function () {
            adminPanel.dataset.open = adminPanel.dataset.open === '1' ? '0' : '1';
            refreshAccessState();
          });

          adminForm.addEventListener('submit', function (event) {
            event.preventDefault();
            if ((adminPassword.value || '').trim() !== ADMIN_PASSWORD) {
              showStatus('That admin password is not correct.', 'error');
              return;
            }

            setStoredValue(ADMIN_KEY, ADMIN_TOKEN, true);
            adminPassword.value = '';
            adminPanel.dataset.open = '1';
            refreshAccessState();
          });

          publicToggle.addEventListener('change', function () {
            if (!hasAdminAccess()) {
              publicToggle.checked = false;
              return;
            }

            if (publicToggle.checked) {
              setStoredValue(PUBLIC_KEY, '1', true);
            } else {
              removeStoredValue(PUBLIC_KEY);
            }

            refreshAccessState();
          });

          adminContinue.addEventListener('click', function () {
            navigateToGame(true);
          });

          adminClear.addEventListener('click', function () {
            removeStoredValue(ADMIN_KEY);
            removeStoredValue(PUBLIC_KEY);
            refreshAccessState();
          });

          refreshAccessState();
        });
      })();
    </script>
  </body>
</html>";

            html = html.Replace("__PWA_SHELL__", BuildPwaShell(buildVersion, "Scribble Wars", "ScribbleWars", false));
            html = html.Replace("__BUILD_VERSION__", buildVersion);
            html = html.Replace("__ACCESS_KEY__", TesterAccessKey);
            html = html.Replace("__ACCESS_TOKEN__", TesterAccessToken);
            html = html.Replace("__TESTER_PASSWORD__", TesterPassword);
            html = html.Replace("__ADMIN_KEY__", AdminAccessKey);
            html = html.Replace("__ADMIN_TOKEN__", AdminAccessToken);
            html = html.Replace("__ADMIN_PASSWORD__", AdminPassword);
            html = html.Replace("__PUBLIC_KEY__", PublicAccessKey);

            File.WriteAllText(Path.Combine(buildPath, "index.html"), html, Encoding.UTF8);
        }

        private static void WriteGamePage(string buildPath, VersionedRuntimeFiles runtimeFiles, string buildVersion)
        {
            string html = @"<!DOCTYPE html>
<html lang=""en-us"">
  <head>
    <meta charset=""utf-8"">
    <meta http-equiv=""Content-Type"" content=""text/html; charset=utf-8"">
    <title>Scribble Wars | Pre-Alpha</title>
    <script>
      (function () {
        const ACCESS_KEY = '__ACCESS_KEY__';
        const ACCESS_TOKEN = '__ACCESS_TOKEN__';
        const ADMIN_KEY = '__ADMIN_KEY__';
        const ADMIN_TOKEN = '__ADMIN_TOKEN__';
        const PUBLIC_KEY = '__PUBLIC_KEY__';
        const params = new URLSearchParams(window.location.search || '');
        const wantsAdmin = params.get('admin') === '1';

        function readStoredValue(key) {
          try {
            return sessionStorage.getItem(key) || localStorage.getItem(key);
          } catch (error) {
            return null;
          }
        }

        function hasTesterAccess() {
          return readStoredValue(ACCESS_KEY) === ACCESS_TOKEN;
        }

        function hasAdminAccess() {
          return readStoredValue(ADMIN_KEY) === ADMIN_TOKEN;
        }

        function publicAccessEnabled() {
          return readStoredValue(PUBLIC_KEY) === '1';
        }

        if (wantsAdmin && !hasAdminAccess()) {
          window.location.replace('./index.html');
          return;
        }

        if (!wantsAdmin && hasAdminAccess()) {
          window.location.replace('./game.html?admin=1');
          return;
        }

        if (!publicAccessEnabled() && !hasTesterAccess() && !hasAdminAccess()) {
          window.location.replace('./index.html');
        }
      })();
    </script>
__PWA_SHELL__
  </head>
  <body style=""text-align:center;padding:0;border:0;margin:0;"">
    <canvas id=""unity-canvas"" width=""1080"" height=""1920"" tabindex=""-1"" style=""width:1080px;height:1920px;background:#807C70""></canvas>
    <script src=""Build/__LOADER__""></script>
    <script>
      if (/iPhone|iPad|iPod|Android/i.test(navigator.userAgent)) {
        var meta = document.createElement('meta');
        meta.name = 'viewport';
        meta.content = 'width=device-width, height=device-height, initial-scale=1.0, user-scalable=no, shrink-to-fit=yes';
        document.getElementsByTagName('head')[0].appendChild(meta);

        var canvas = document.querySelector('#unity-canvas');
        canvas.style.width = '100%';
        canvas.style.height = '100%';
        canvas.style.position = 'fixed';

        document.body.style.textAlign = 'left';
      }

      createUnityInstance(document.querySelector('#unity-canvas'), {
        arguments: [],
        dataUrl: 'Build/__DATA__',
        frameworkUrl: 'Build/__FRAMEWORK__',
        codeUrl: 'Build/__WASM__',
        streamingAssetsUrl: 'StreamingAssets',
        companyName: 'PlaysBad',
        productName: 'Scribble Wars',
        productVersion: '1.0'
      }).then((unityInstance) => {
      }).catch((message) => {
        alert(message);
      });
    </script>
  </body>
</html>";

            html = html.Replace("__ACCESS_KEY__", TesterAccessKey);
            html = html.Replace("__ACCESS_TOKEN__", TesterAccessToken);
            html = html.Replace("__ADMIN_KEY__", AdminAccessKey);
            html = html.Replace("__ADMIN_TOKEN__", AdminAccessToken);
            html = html.Replace("__PUBLIC_KEY__", PublicAccessKey);
            html = html.Replace("__PWA_SHELL__", BuildPwaShell(buildVersion, "Scribble Wars", "ScribbleWars", true));
            html = html.Replace("__LOADER__", runtimeFiles.LoaderFileName);
            html = html.Replace("__DATA__", runtimeFiles.DataFileName);
            html = html.Replace("__FRAMEWORK__", runtimeFiles.FrameworkFileName);
            html = html.Replace("__WASM__", runtimeFiles.WasmFileName);

            File.WriteAllText(Path.Combine(buildPath, "game.html"), html, Encoding.UTF8);
        }

        private static string BuildPwaShell(string buildVersion, string appTitle, string logPrefix, bool includeAlias)
        {
            string shell = @"
    <link rel=""manifest"" href=""__MANIFEST__?v=__VERSION__"">
    <meta name=""theme-color"" content=""#f4ead6"">
    <meta name=""mobile-web-app-capable"" content=""yes"">
    <meta name=""apple-mobile-web-app-capable"" content=""yes"">
    <meta name=""apple-mobile-web-app-status-bar-style"" content=""default"">
    <meta name=""apple-mobile-web-app-title"" content=""__APP_TITLE__"">
    <link rel=""apple-touch-icon"" href=""__ICON__?v=__VERSION__"">
    <script>
      __PWA_ASSIGNMENT__
        let deferredPrompt = null;

        function canRegisterServiceWorker() {
          return 'serviceWorker' in navigator
            && (window.location.protocol === 'https:' || window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1');
        }

        window.addEventListener('beforeinstallprompt', function (event) {
          event.preventDefault();
          deferredPrompt = event;
        });

        window.addEventListener('appinstalled', function () {
          deferredPrompt = null;
        });

        async function requestInstall() {
          const isStandalone = window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true;
          const isIos = /iphone|ipad|ipod/i.test(window.navigator.userAgent || '');

          if (deferredPrompt) {
            deferredPrompt.prompt();
            try {
              await deferredPrompt.userChoice;
            } catch (error) {
              console.warn('[__LOG_PREFIX__PWA] Install prompt failed.', error);
            }
            return true;
          }

          if (isIos && !isStandalone) {
            window.alert('To install __APP_TITLE__ on iPhone or iPad, tap Share and then Add to Home Screen.');
            return false;
          }

          if (window.location.protocol !== 'https:' && window.location.hostname !== 'localhost' && window.location.hostname !== '127.0.0.1') {
            window.alert('Install requires HTTPS on mobile browsers. Serve this WebGL build over HTTPS and try again.');
            return false;
          }

          window.alert('Install is not ready yet in this browser. Try again after the page finishes loading, or use the browser menu to install/add to home screen.');
          return false;
        }

        async function registerServiceWorker() {
          if (!canRegisterServiceWorker()) {
            return;
          }

          try {
            await navigator.serviceWorker.register('./__SERVICE_WORKER__?v=__VERSION__');
          } catch (error) {
            console.warn('[__LOG_PREFIX__PWA] Service worker registration failed.', error);
          }
        }

        registerServiceWorker();

        return {
          requestInstall: requestInstall
        };
      })();
    </script>";

            shell = shell.Replace("__MANIFEST__", ManifestFileName);
            shell = shell.Replace("__VERSION__", buildVersion);
            shell = shell.Replace("__APP_TITLE__", appTitle);
            shell = shell.Replace("__ICON__", Icon192FileName);
            shell = shell.Replace("__SERVICE_WORKER__", ServiceWorkerFileName);
            shell = shell.Replace("__LOG_PREFIX__", logPrefix);
            shell = shell.Replace("__PWA_ASSIGNMENT__", includeAlias
                ? "window.CardzPWA = window.ScribbleWarsPWA = (function () {"
                : "window.CardzPWA = (function () {");

            return shell;
        }

        private static void WriteManifest(string buildPath)
        {
            string manifestPath = Path.Combine(buildPath, ManifestFileName);
            string manifest = @"{
  ""name"": ""Scribble Wars"",
  ""short_name"": ""Scribble Wars"",
  ""start_url"": ""./index.html"",
  ""scope"": ""./"",
  ""display"": ""standalone"",
  ""orientation"": ""portrait"",
  ""background_color"": ""#f4ead6"",
  ""theme_color"": ""#f4ead6"",
  ""description"": ""Scribble Wars private pre-alpha test build."",
  ""icons"": [
    {
      ""src"": ""./pwa-icon-192.svg"",
      ""sizes"": ""192x192"",
      ""type"": ""image/svg+xml"",
      ""purpose"": ""any maskable""
    },
    {
      ""src"": ""./pwa-icon-512.svg"",
      ""sizes"": ""512x512"",
      ""type"": ""image/svg+xml"",
      ""purpose"": ""any maskable""
    }
  ]
}";

            File.WriteAllText(manifestPath, manifest, Encoding.UTF8);
        }

        private static void WriteServiceWorker(string buildPath, string buildVersion)
        {
            string serviceWorkerPath = Path.Combine(buildPath, ServiceWorkerFileName);
            string serviceWorker = $@"const CACHE_NAME = 'scribblewars-shell-{buildVersion}';

function isUnityRuntimeRequest(requestUrl) {{
  try {{
    const url = new URL(requestUrl);
    return url.origin === self.location.origin
      && (url.pathname.includes('/Build/')
        || url.pathname.includes('/StreamingAssets/')
        || url.pathname.endsWith('.data')
        || url.pathname.endsWith('.wasm')
        || url.pathname.endsWith('.js'));
  }} catch (error) {{
    return false;
  }}
}}

self.addEventListener('install', event => {{
  event.waitUntil(self.skipWaiting());
}});

self.addEventListener('activate', event => {{
  event.waitUntil((async () => {{
    const keys = await caches.keys();
    await Promise.all(keys
      .filter(key => (key.startsWith('cardz-') || key.startsWith('scribblewars-')) && key !== CACHE_NAME)
      .map(key => caches.delete(key)));
    await self.clients.claim();
  }})());
}});

self.addEventListener('fetch', event => {{
  if (event.request.method !== 'GET') {{
    return;
  }}

  if (isUnityRuntimeRequest(event.request.url)) {{
    event.respondWith(fetch(event.request, {{ cache: 'no-store' }}));
    return;
  }}

  event.respondWith((async () => {{
    const cache = await caches.open(CACHE_NAME);
    try {{
      const response = await fetch(event.request, {{ cache: 'no-store' }});
      const contentType = response && response.headers ? (response.headers.get('content-type') || '') : '';
      if (response
        && response.ok
        && event.request.url.startsWith(self.location.origin)
        && !isUnityRuntimeRequest(event.request.url)
        && (contentType.includes('text/html')
          || contentType.includes('text/css')
          || contentType.includes('javascript')
          || contentType.includes('image/')
          || contentType.includes('application/manifest+json')))
      {{
        cache.put(event.request, response.clone());
      }}
      return response;
    }} catch (error) {{
      const cached = await cache.match(event.request, {{ ignoreSearch: true }});
      if (cached) {{
        return cached;
      }}
      throw error;
    }}
  }})());
}});";

            File.WriteAllText(serviceWorkerPath, serviceWorker, Encoding.UTF8);
        }

        private static void DeleteLegacyServiceWorker(string buildPath)
        {
            string legacyPath = Path.Combine(buildPath, LegacyServiceWorkerFileName);
            if (File.Exists(legacyPath))
            {
                File.Delete(legacyPath);
            }
        }

        private static void WriteWebConfig(string buildPath)
        {
            string webConfigPath = Path.Combine(buildPath, WebConfigFileName);
            string webConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <system.webServer>
    <staticContent>
      <remove fileExtension="".wasm"" />
      <remove fileExtension="".data"" />
      <remove fileExtension="".webmanifest"" />
      <remove fileExtension="".json"" />
      <remove fileExtension="".js"" />
      <remove fileExtension="".svg"" />
      <mimeMap fileExtension="".wasm"" mimeType=""application/wasm"" />
      <mimeMap fileExtension="".data"" mimeType=""application/octet-stream"" />
      <mimeMap fileExtension="".webmanifest"" mimeType=""application/manifest+json"" />
      <mimeMap fileExtension="".json"" mimeType=""application/json"" />
      <mimeMap fileExtension="".js"" mimeType=""application/javascript"" />
      <mimeMap fileExtension="".svg"" mimeType=""image/svg+xml"" />
    </staticContent>
  </system.webServer>
  <location path=""index.html"">
    <system.webServer>
      <httpProtocol>
        <customHeaders>
          <add name=""Cache-Control"" value=""no-cache, no-store, must-revalidate"" />
          <add name=""Pragma"" value=""no-cache"" />
          <add name=""Expires"" value=""0"" />
        </customHeaders>
      </httpProtocol>
    </system.webServer>
  </location>
  <location path=""game.html"">
    <system.webServer>
      <httpProtocol>
        <customHeaders>
          <add name=""Cache-Control"" value=""no-cache, no-store, must-revalidate"" />
          <add name=""Pragma"" value=""no-cache"" />
          <add name=""Expires"" value=""0"" />
        </customHeaders>
      </httpProtocol>
    </system.webServer>
  </location>
  <location path=""manifest.webmanifest"">
    <system.webServer>
      <httpProtocol>
        <customHeaders>
          <add name=""Cache-Control"" value=""no-cache, no-store, must-revalidate"" />
        </customHeaders>
      </httpProtocol>
    </system.webServer>
  </location>
  <location path=""sw.js"">
    <system.webServer>
      <httpProtocol>
        <customHeaders>
          <add name=""Cache-Control"" value=""no-cache, no-store, must-revalidate"" />
        </customHeaders>
      </httpProtocol>
    </system.webServer>
  </location>
</configuration>";

            File.WriteAllText(webConfigPath, webConfig, Encoding.UTF8);
        }

        private static void WriteIcon(string buildPath, string fileName)
        {
            string iconPath = Path.Combine(buildPath, fileName);
            string svg = @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 512 512"">
  <rect width=""512"" height=""512"" rx=""96"" fill=""#f4ead6"" />
  <rect x=""32"" y=""32"" width=""448"" height=""448"" rx=""82"" fill=""#fffaf0"" stroke=""#333333"" stroke-width=""16"" />
  <path d=""M96 140h320"" stroke=""#c2d5ff"" stroke-width=""12"" stroke-linecap=""round"" />
  <path d=""M96 188h320"" stroke=""#c2d5ff"" stroke-width=""12"" stroke-linecap=""round"" />
  <path d=""M96 236h320"" stroke=""#c2d5ff"" stroke-width=""12"" stroke-linecap=""round"" />
  <text x=""256"" y=""320"" text-anchor=""middle"" font-family=""Arial, sans-serif"" font-size=""150"" font-weight=""700"" fill=""#333333"">SW</text>
  <circle cx=""420"" cy=""96"" r=""44"" fill=""#ffd84a"" stroke=""#333333"" stroke-width=""10"" />
</svg>";

            File.WriteAllText(iconPath, svg, Encoding.UTF8);
        }

        private static void WriteLandingLogo(string buildPath)
        {
            string outputPath = Path.Combine(buildPath, LandingLogoFileName);
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
            string sourcePath = Path.Combine(projectRoot, LandingLogoSourceRelativePath.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(sourcePath))
            {
                Debug.LogWarning($"[Cardz][PWA] Landing logo source not found: {sourcePath}");
                return;
            }

            File.Copy(sourcePath, outputPath, true);
        }
    }
}
