This is a modded version of the patcher from the original Bartizan project, just for resource patching. Currently, it can:
- add or replace sprites in texture atlases by looking for free space or creating it if necessary
- add or replace XML elements for files such as the theme or tileset data
- recognize external modifications (for example, updates) and react accordingly

TO DO:
- do hash checking against "official" version of the atlas XMLs, and hardcode free space for those. Saves time for unmodded installations of TFA (I don't think this is actually neccessary, it's pretty fast)
- music patching? Is that even possible? If not just do a simple binary patcher, w/e lol
- file replacement (can toggle between original and modded file in the patcher)
- file download? in case you don't want to include a huge file in the distributed archive
- make it a full blown mod manager
- - split up patchfiles into seperate folders for distributing mods
- - allow execution of scripts to enable/disable mods
- - automatic savefile managment + backups