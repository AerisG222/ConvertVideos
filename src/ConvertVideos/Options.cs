using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;

namespace ConvertVideos;

public class Options
{
    CategoryInfo _category;

    public string CategoryName { get; private set; }
    public FileInfo OutputFile { get; private set; }
    public DirectoryInfo VideoDirectory { get; private set; }
    public string WebDirectory { get; private set; }
    public string[] AllowedRoles { get; private set; }
    public ushort Year { get; private set; }

    public CategoryInfo CategoryInfo
    {
        get
        {
            if (_category == null)
            {
                _category = new CategoryInfo
                {
                    Name = CategoryName,
                    Year = Year,
                    AllowedRoles = AllowedRoles
                };
            }

            return _category;
        }
    }

    public void Parse(string[] args)
    {
        var rootCommand = BuildRootCommand();

        rootCommand.Invoke(args);

        var errors = ValidateOptions();

        if (errors.Any())
        {
            Console.WriteLine("Errors processing options:");

            foreach (var err in errors)
            {
                Console.WriteLine($"  - {err}");
            }

            Console.WriteLine("Exiting");

            Environment.Exit(1);
        }
    }

    IEnumerable<string> ValidateOptions()
    {
        var errors = new List<string>();

        if (OutputFile == null)
        {
            errors.Add("You must specify an output file.");
        }

        if (VideoDirectory == null)
        {
            errors.Add("You must specify the video directory.");
        }

        if (string.IsNullOrWhiteSpace(WebDirectory))
        {
            errors.Add("You must specify the web directory.");
        }

        if (string.IsNullOrWhiteSpace(CategoryName))
        {
            errors.Add("Please specify the category name.");
        }

        if (Year == 0)
        {
            errors.Add("Please specify the year.");
        }

        if (AllowedRoles == null || !AllowedRoles.Any())
        {
            errors.Add("Please specify at least one role.");
        }

        return errors;
    }

    RootCommand BuildRootCommand()
    {
        var categoryNameOption = new Option<string>(new[] { "-c", "--category-name" }, "The name for the category to represent the videos in the directory.");
        var outputFileOption = new Option<FileInfo>(new[] { "-o", "--output-file" }, "The path to the SQL file to generate.");
        var videoDirectoryOption = new Option<DirectoryInfo>(new[] { "-v", "--video-directory" }, "The directory containing the source videos to resize: (/home/mmorano/Desktop/mypix/).");
        var webDirectoryOption = new Option<string>(new[] { "-w", "--web-directory" }, "The full URL path to the image directory: (/images/2009/mypix/).");
        var allowedRolesOption = new Option<string[]>(new[] { "-r", "--allowed-roles" }, "Roles that will have access to this category");
        var yearOption = new Option<ushort>(new[] { "-y", "--year" }, "The year the pictures were taken.");

        var rootCommand = new RootCommand("A utility to scale videos to be shown on mikeandwan.us")
            {
                categoryNameOption,
                outputFileOption,
                videoDirectoryOption,
                webDirectoryOption,
                allowedRolesOption,
                yearOption
            };

        rootCommand.SetHandler((
            string categoryName,
            FileInfo outputFile,
            DirectoryInfo videoDirectory,
            string webDirectory,
            string[] allowedRoles,
            ushort year) =>
        {
            CategoryName = categoryName;
            OutputFile = outputFile;
            VideoDirectory = videoDirectory;
            WebDirectory = webDirectory;
            AllowedRoles = allowedRoles;
            Year = year;
        },
            categoryNameOption,
            outputFileOption,
            videoDirectoryOption,
            webDirectoryOption,
            allowedRolesOption,
            yearOption
        );

        return rootCommand;
    }
}
