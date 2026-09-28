mergeInto(LibraryManager.library, {
  SyncSaveFilesToIndexedDB: function () {
    FS.syncfs(false, function (err) {
      if (err) console.error("[SaveManager] Failed to sync save files to IndexedDB: " + err);
    });
  }
});
