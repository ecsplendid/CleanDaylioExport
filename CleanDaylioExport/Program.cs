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
        private const string ContainsPipeList = @",[^,]+?\|[^,]+?(?=,)";
        
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
                .Where(l => Regex.IsMatch(l, ContainsPipeList));

            var activities = metadataLines
                // get the pipe delimited tag bit
                .Select(l => Regex.Match(l, ContainsPipeList).Value)
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
                            Regex.Match(line, ContainsPipeList).Value,
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

            const string OutputFile = "flatmatrix.csv";

            File.Delete(OutputFile);

            // for each day, write out all the attributes to a file

            // write out the headers

            File.AppendAllText(
                OutputFile,
                $"date, mood, {string.Join(",", moods)}, {string.Join(",", activities)}\r\n"
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

                File.AppendAllText(OutputFile, day.ToShortDateString() );
                File.AppendAllText(OutputFile, "," );
                File.AppendAllText(OutputFile, GetMoodCode( attributes["mood"].Data[day], moods ) );
                File.AppendAllText(OutputFile, "," );
                File.AppendAllText(OutputFile, string.Join(",", modeBits));
                File.AppendAllText(OutputFile, ",");
                File.AppendAllText(OutputFile, string.Join( ",", activityMap ) );
                File.AppendAllText(OutputFile,"\r\n");
            }

            // some ideas about how to build the correlation matrix
            // go through every attribute squared, build up a matrix of correlations
            // to build a correlation simply go through every day and score 1 for a pair
            // of matching attributes, then normalize by the number of days that attribute has 
            // been in play, which is calculated by finding the first date the attribute was used.
            // a parameter of the algorithm could be to include a match on vicinities (past/present etc)
            // then generate a gephi chart for the matrix, first all of the values on the matrix
            // will need to be lin scaled onto the interval [0,1]

            var matrix = new Dictionary<string, double>();

            var highest = double.MinValue;

            foreach(var a in activities)
            {
                // use heuristic to figure out how long the attribute has been in play
                // how long has attribute 1 been in play?
                var a1InPlay = days.FindIndex(d => attributes[a].Data.ContainsKey(d)); 
                
                foreach (var a2 in activities)
                {
                    // how long has attribute 1 been in play?
                    var a2InPlay = days.FindIndex(d => attributes[a2].Data.ContainsKey(d));

                    var laterDayIndex = Math.Max( a1InPlay, a2InPlay );
                    
                   // note we will normalize by the days in play
                    var daysInPlay = days.Count() - laterDayIndex;

                    // for every day in play build up a count
                    var normalizedValue =
                        (double) 
                        Enumerable
                        .Range(laterDayIndex, daysInPlay)
                        .Select(i => days[i])
                        .Select(d =>
                           attributes[a2].Data.ContainsKey(d)
                               && attributes[a].Data.ContainsKey(d))
                        .Count(b => b) / daysInPlay;
                    
                    matrix.Add( $"{a}_{a2}",  normalizedValue );

                    highest = Math.Max(highest, normalizedValue);
                    
                }
            }

            //normalize the values onto the [0,1] interval
            foreach(var cell in matrix.Keys.ToArray())
            {
                matrix[cell] = matrix[cell] / highest; 
            }

            // write out this matrix into Gephi format

            const string GephiFileName = "gephi-matrix.txt";

            if(File.Exists(GephiFileName))
                File.Delete(GephiFileName);

            File.AppendAllText(GephiFileName, $";{string.Join(";",activities)}\r\n" );

            foreach ( var activity in activities)
            {
                var vals = activities
                    .Select( a => matrix[$"{ activity }_{a}"] );

                File.AppendAllText(GephiFileName, $"{activity};{string.Join(";", vals )}\r\n");
            }

        }

        public static string GetMoodCode(string moodString, string[] moods)
        {
            return $"{Array.FindIndex(moods, m => m == moodString)}";
        }
    }
}
