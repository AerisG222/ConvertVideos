using System;
using System.CommandLine;


namespace ConvertVideos
{
    public class Options
    {
        bool _help;
        string _catname;
        string _outfile;
        string _viddir;
        string _webdir;
        bool _isPrivate;
        int _year;

        public string CategoryName { get { return _catname; } }
        public string OutputFile { get { return _outfile; } }
        public string VideoDirectory { get { return _viddir; } }
        public string WebDirectory  { get { return _webdir; } }
        public bool IsPrivate { get { return _isPrivate; } }
        public int Year { get { return _year; } }


        public void Parse(string[] args)
        {
            ArgumentSyntax.Parse(args, syntax =>
            {
                syntax.ApplicationName = "ConvertVideos";

                syntax.HandleHelp = false;

                syntax.DefineOption("h|help", ref _help, "This help screen.");
                syntax.DefineOption("c|catname", ref _catname, "The name for the category to represent the videos in the directory.");
                syntax.DefineOption("o|outfile", ref _outfile, "The path to the SQL file to generate.");
                syntax.DefineOption("v|viddir", ref _viddir, "The directory containing the source videos to resize: (/home/mmorano/Desktop/mypix/).");
                syntax.DefineOption("w|webdir", ref _webdir, "The full URL path to the image directory: (/images/2009/mypix/).");
                syntax.DefineOption("x|private", ref _isPrivate, "Mark the category as private so only the admin can view these pictures.");
                syntax.DefineOption("y|year", ref _year, "The year the pictures were taken.");

                if(_help)
                {
                    Console.WriteLine(syntax.GetHelpText());
                    Environment.Exit(0);
                }
                else
                {
                    ValidateOptions(syntax);
                }
            });
        }


        public void ValidateOptions(ArgumentSyntax syntax)
        {
            if(string.IsNullOrWhiteSpace(OutputFile))
            {
                syntax.ReportError("You must specify an output file.");
            }
		    
            if(string.IsNullOrWhiteSpace(VideoDirectory))
            {
                syntax.ReportError("You must specify the video directory.");
            }

            if(string.IsNullOrWhiteSpace(WebDirectory))
            {
                syntax.ReportError("You must specify the web directory.");
            }

            if(string.IsNullOrWhiteSpace(CategoryName))
            {
                syntax.ReportError("Please specify the category name.");
            }

			if(Year == 0)
			{
				syntax.ReportError("Please specify the year.");
			}
        }
    }
}
