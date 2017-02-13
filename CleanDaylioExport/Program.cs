using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CleanDaylio
{
    class Program
    {
        private const string containsPipeList = @",[^,]+?\|[^,]+?(?=,)";
        
        static void Main(string[] args)     
        {
            // these can be parameterised
            var moods = new[] {
                "fugly",
                "awful",
                "meh",
                "good",
                "rad"
            };

            var file = File
                .ReadAllLines("daylio.txt")
                .Skip(1);

            // first thing we will strip out a distinct list of tags
            var metadataLines = file
                // dont want the headings
                .Skip(1)
                .Where(l => Regex.IsMatch(l, containsPipeList));

            var activities = metadataLines
                // get the pipe delimited tag bit
                .Select(l => Regex.Match(l, containsPipeList).Value)
                .SelectMany(c => Regex.Split(c, @"\|"))
                .Select( w => w.Replace(",", string.Empty).Trim() )
                .Distinct()
                .ToList();

            // use this data structure for assigning the data
            var attributes = activities
                .Select(t => new Attribute { Name = t })
                .ToDictionary( a => a.Name, a => a );

            // manually add one in for the mood
            attributes.Add("mood", new Attribute { Name = "mood" });

            // now let's go through the data and add/reference the attributes 
            foreach ( var line in metadataLines)
            {
                var sl = line.Split(',');
                var date = DateTime.Parse( $"00:00 {sl[1]} {sl[0]}" );

                var atts =
                    Regex
                        .Split(
                            Regex.Match(line, containsPipeList).Value,
                            @"\|")
                        // clean the tags up
                        .Select(w => w.Replace(",", string.Empty).Trim())
                        .Select(a => attributes[a]);

                foreach (var att in atts.Where(
                    att => !att.Data.ContainsKey(date)))
                {
                    att.Data.Add(date, string.Empty);
                }

                // do the mood bit too
                if (!attributes["mood"].Data.ContainsKey(date))
                    attributes["mood"].Data.Add(date, sl[3]);
            }
             
            // last piece of the puzzle
            // we need to know all of the distinct dates to select out

            var days = attributes
                .Values
                .SelectMany(v => v.Data.Keys)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            // delete the output file if it exists

            const string outputFile = "flatmatrix.csv";

            File.Delete(outputFile);

            // for each day, write out all the attributes to a file

            // write out the headers

            File.AppendAllText(
                outputFile,
                $"date, mood, {String.Join(",", moods)}, {String.Join(",", activities)}\r\n"
                ); 

            foreach ( var day in days)
            {
                var activityMap = activities
                        .Select(a => attributes[a].Data.ContainsKey(day)
                                        ? "1":"0" );

                var modeBits = 
                    moods.Select(mood => attributes["mood"].Data.ContainsKey(day)
                                        && attributes["mood"].Data[day] == mood
                                        ? "1" : "0");

                File.AppendAllText(outputFile, day.ToShortDateString() );
                File.AppendAllText(outputFile, "," );
                File.AppendAllText(outputFile, GetMoodCode( attributes["mood"].Data[day], moods ) );
                File.AppendAllText(outputFile, "," );
                File.AppendAllText(outputFile, String.Join(",", modeBits));
                File.AppendAllText(outputFile, ",");
                File.AppendAllText(outputFile, String.Join( ",", activityMap ) );
                File.AppendAllText(outputFile,"\r\n");
            }

            // some ideas about how to build the correlation matrix
            // go through every attribute squared, build up a matrix of correlations
            // to build a correlation simply go through every day and score 1 for a pair
            // of matching attributes, then normalize by the number of days that attribute has 
            // been in play, which is calculated by finding the first date the attribute was used.
            // a parameter of the algorithm could be to include a match on vicinities (past/present etc)
            // then generate a gephi chart for the matrix, first all of the values on the matrix
            // will need to be lin scaled onto the interval [0,1]

            foreach


        }

        public static string GetMoodCode(string moodString, string[] moods)
        {
            return $"{Array.FindIndex(moods, m => m == moodString)}";
        }
    }
}
