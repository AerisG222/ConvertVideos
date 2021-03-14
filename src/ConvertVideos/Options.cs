using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;

namespace ConvertVideos
{
    public class Options
    {
        public string CategoryName { get; private set; }
        public FileInfo OutputFile { get; private set; }
        public DirectoryInfo VideoDirectory { get; private set; }
        public string WebDirectory  { get; private set; }
        public bool IsPrivate { get; private set; }
        public int Year { get; private set; }

        public void Parse(string[] args)
        {
            var rootCommand = BuildRootCommand();

            rootCommand.Invoke(args);

            var errors = ValidateOptions();

            if(errors.Any())
            {
                Console.WriteLine("Errors processing options:");

                foreach(var err in errors)
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

            if(OutputFile == null)
            {
                errors.Add("You must specify an output file.");
            }

            if(VideoDirectory == null)
            {
                errors.Add("You must specify the video directory.");
            }

            if(string.IsNullOrWhiteSpace(WebDirectory))
            {
                errors.Add("You must specify the web directory.");
            }

            if(string.IsNullOrWhiteSpace(CategoryName))
            {
                errors.Add("Please specify the category name.");
            }

			if(Year == 0)
			{
				errors.Add("Please specify the year.");
			}

            return errors;
        }

        RootCommand BuildRootCommand()
        {
            var rootCommand = new RootCommand
            {
                new Option<string>(
                    new string[] {"-c", "--category-name"},
                    "The name for the category to represent the videos in the directory."
                ),
                new Option<FileInfo>(
                    new string[] {"-o", "--output-file"},
                    "The path to the SQL file to generate."
                ),
                new Option<DirectoryInfo>(
                    new string[] {"-v", "--video-directory"},
                    "The directory containing the source videos to resize: (/home/mmorano/Desktop/mypix/)."
                ),
                new Option<string>(
                    new string[] {"-w", "--web-directory"},
                    "The full URL path to the image directory: (/images/2009/mypix/)."
                ),
                new Option<bool>(
                    new string[] {"-x", "--is-private"},
                    "Mark the category as private so only the admin can view these pictures."
                ),
                new Option<int>(
                    new string[] {"-y", "--year"},
                    "The year the pictures were taken."
                )
            };

            rootCommand.Description = "A utility to scale videos to be shown on mikeandwan.us";

            rootCommand.Handler = CommandHandler.Create<string, FileInfo, DirectoryInfo, string, bool, int>(
                (categoryName, outputFile, videoDirectory, webDirectory, isPrivate, year) => {
                    CategoryName = categoryName;
                    OutputFile = outputFile;
                    VideoDirectory = videoDirectory;
                    WebDirectory = webDirectory;
                    IsPrivate = isPrivate;
                    Year = year;
                }
            );

            return rootCommand;
        }
    }
}
