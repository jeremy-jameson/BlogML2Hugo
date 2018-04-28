BlogML to Hugo
===================
This tool helps you convert your exported BlogML xml file to Hugo friendly markdown files. 
Many blog engines can generate BlogML files. If you want to migrate your blog to hugo, this tool may be helpful for you.


Usage
-----------

Some prebuilt binaries are ready for Windows and macOS in the [release section](https://github.com/jijiechen/BlogML2Hugo/releases), but they may not work as OS upgrades. If so, please follow following steps in the `Running from source` paragraph to built it by yourself.

Open a command line prompt / terminal / PowerShell, and type in the following commandline (without the $):

```sh
    $ cd BlogML2Hugo-osx
    $ ./BlogML2Hugo <path-to-blogml.xml> <path-of-content-post-in-hugo>
```

Wait for completion.


Notes
-----------

This tool only converts the exported xml (which is in BlogML format) to multiple hugo-compatible markdown files. It does not fix links to attachments or images in your existing posts. So you'll need some mannual steps to take care of those.


Running from source
-----------

Before running, you need to compile the application from this source. You'll need the [.NET Core command line tool](https://www.microsoft.com/net/download) to compile the application.

```sh
    $ dotnet build
```

- Run the application in your command line prompt / terminal.

```sh
    $ cd BlogML2Hugo
    $ dotnet run <path-to-blogml.xml> <path-of-content-post-in-hugo>
```


License
-----------
This code is licensed under the [MIT lincense](https://opensource.org/licenses/MIT).


