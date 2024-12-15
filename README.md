# QuickStack

For more details about this mod, please refer to the [mod page](https://www.nexusmods.com/7daystodie/mods/1357).

## Build

Prerequisites:
- Visual Studio (using 2022, haven't tested with 2019)
- .NET Framework 4.8

1. Go to your Mods folder. Usually located in "C:\Users\<USER>\AppData\Roaming\7DaysToDie\Mods"
2. Open git bash and run: git clone git@github.com:Westwud1/QuickStack.git
3. This repository also includes the .dll file, so the mod is technically installed and you should be able to use it. The other files (visual studio settings or build files) do not affect the mod.
4. Set the environment variable "SDTD_DIR" to your 7 days to die directory. This is used for the project to be able to correctly find the dependencies.
5. Open QuickStack.sln and build the QuickStack library.

If you have any issues, please refer to [this](https://www.youtube.com/watch?v=n463fVZ26tY&list=PLJeCuPbkcF5RhAOkX7ghThq7hIfZ5Ypgj&index=3&ab_channel=SphereII) youtube video.
