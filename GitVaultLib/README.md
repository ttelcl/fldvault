# GitVaultLib

Library for keeping backups of your git repositories in a vault.
The basic idea is to create git bundles and pack then into an MVLT file.

## Principles

- Each repository is related to the following folders
	- The actual git repository (be it a bare repository or a working copy)
	- A folder on your local nachine hosting the bundle files
	  - Your local machine writes a bundle file tagged to your machine there
		- Bundles from other machines are extracted there
	- A folder on a cloud-backed folder on your machine holding vaults for
	  the bundles. For each repository, such a folder is created within
		an 'anchor' folder that you define when setting up gitvault. You can have
		multiple anchor folders on your disk, for instance for different cloud
		providers.
- Each vault / bundle is identified by the following parts that together
  are unique:
	- The name of the repository
	- The name of the host machine. You can use the hostname or a
		custom name. Custom names are useful to distinguish multiple copies of a
		repo on the same machine.
- A central setting system stores settings common to all repositories on your
  machine.
	- The list of anchor folders you have defined
	- The default bundle folder
	- The default host name, if different from your actual host name.
	  You may wish to set a different default name, since the 'host' name
		is used to construct bundle and vault names, which will be shared on your
		cloud folder.
- Each git repository used with this library has a configuration file in
  the .git folder that stores the following information:
	- The logical name of the repository (to make sure the same name is used
	  on all machines)
	- The name used to identify your computer (the 'host')
	- The folder for the bundle files for this repository (subfolder of a
	  global 'bundle folder')
	- The folder for the vault files for this repository (subfolder of an
	  anchor folder)
	- Vault key information.
