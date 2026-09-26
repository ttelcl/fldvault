# KeyLoader

This application acts as a short lived front-frontend in front of
ZVaultKeyServer. Its purposes are:

* Populate keys in the server
* Manage master key lists
* Act as an intermediary for "opening" various key related file types

## Provided functions

Each functionality is presented in a separate tab (separate tabs
also for independent starts of the "same" task).

These functions are disabled if no key server is running. Consider
adding the option to start the key server from this loader app.

### Load a master key file into the server

* Select a master key file to load
* Enter the master keyphrase and validate it
  * Do NOT look up the master key from the server.
    Require manual entry.
* Read the content of the master key file intp memory
  (reading both key transforms and key infos)
* Present a list of the keys loaded, with check boxes to select
  what to upload (separate for key and info)
* Indicate which keys are already present in the server
* Keys can be uploaded selectively or in bulk
* The user interface can be switched to "Edit Mode".
    * (_continue at the 'continue editing' section below_)
* Otherwise clean up and close the task tab. No need to save
  if there are no changes.

### Edit a master key file

* Start by loading the master key file as above
* Switch to "Edit Mode"
* (_continue at the 'continue editing' section below_)

### Create a new master key file

* Note: Master keys should not be reused, each master key file
  has its own raw key. Passphrases should not be reused either,
  but if they are they should result in distinct raw keys because
  of distinct salts.
* Enter a NEW master keyphrase
* Enter it again to catch typos, and validate it is equal
* Create a new random key from the passphrase
* Ask for a file name to save to
* Save the empty master key file
* (_continue at the 'continue editing' section below_)

### Continue editing a loaded key file

* (_This point in the flow is reached from either opening an existing
  file or creating a new one_)
* When both aspects are missing the key is in a ghost state where it
  can be removed completely. Removal from the list is not automatic,
  but saving the list skips such keys.
* Allow adding (or updating) a key by pasting a ZKEY record
  (if missing, the raw key can be looked up in the server)
* Allow adding a key from a *.key-info, *.zvlt or *.mvlt file
  (the raw key can be looked up in the server)
* Allow adding a key by providing the key id of a key existing in
  the key server.
* For keys that have the info but not the key: provide passphrase
  entry
* Allow creating a new random raw key without passphrase. Such keys
  can ONLY be loaded from a master key file.
* Allow copying a key record from one open key file into one open
  for editing
* Save. Optionally switch back to "Load Mode" if there are no changes.
* Close the file and task tab

### Support other key load methods

* Other key load methods, not depending on master key files
  can be implemented via their own task tabs.
