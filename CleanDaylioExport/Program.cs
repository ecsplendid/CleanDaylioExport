using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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
            // these can be parametrized
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
            var data = activities
                .Select(t => new Attribute { Name = t })
                .ToDictionary( a => a.Name, a => a );

            // manually add one in for the mood
            data.Add("mood", new Attribute { Name = "mood" });

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
                        .Select(a => data[a]);

                foreach (var att in atts.Where(
                    att => !att.Data.ContainsKey(date)))
                {
                    att.Data.Add(date, string.Empty);
                }

                // do the mood bit too
                if (!data["mood"].Data.ContainsKey(date))
                    data["mood"].Data.Add(date, sl[4]);
            }
             
            // last piece of the puzzle
            // we need to know all of the distinct dates to select out

            var days = data
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
                        .Select(a => data[a].Data.ContainsKey(day)
                                        ? "1":"0" );

                var modeBits = 
                    moods.Select(mood => data["mood"].Data.ContainsKey(day)
                                        && data["mood"].Data[day] == mood
                                        ? "1" : "0");

                File.AppendAllText(OutputFile, day.ToShortDateString() );
                File.AppendAllText(OutputFile, "," );
                File.AppendAllText(OutputFile, GetMoodCode( data["mood"].Data[day], moods ) );
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

            foreach(var a1 in activities)
            {
                // use heuristic to figure out how long the attribute has been in play
                // how long has attribute 1 been in play?
                var a1DaysInPlay = GetDaysInPlay(
                    days,
                    data,
                    a1); 
                
                foreach (var a2 in activities.Where(a => a != a1))
                {
                    // how long has attribute 1 been in play?
                    var a2DaysInPlay = GetDaysInPlay(
                    days,
                    data,
                    a2);

                    // need to know the earlier pair of dates in the two sequences
                    // so any date before this we discard
                    var allDays = a1DaysInPlay
                        .Concat(a2DaysInPlay)
                        .ToList();

                    var startFrom = allDays
                        .OrderBy(d => d)
                        .GroupBy(d => d)
                        .FirstOrDefault(g => g.Count() > 1)
                        ?.Key;

                    if (startFrom == null)
                        continue;
                    
                    // for every day in play build up a count
                    var daysInPlay = allDays
                        .Distinct()
                        .Where(d => d >= startFrom)
                        .ToArray();

                    if(!daysInPlay.Any())
                        continue;

                    var numberMatchingDays = daysInPlay
                        .Count(d =>
                           data[a2].Data.ContainsKey(d)
                               && data[a1].Data.ContainsKey(d));
                    
                    var normalizedValue =
                        (double)
                        numberMatchingDays / daysInPlay.Count();
                    
                    matrix.Add( $"{a1}_{a2}",  normalizedValue );

                    highest = Math.Max(highest, normalizedValue);
                    
                }
            }

            //normalize the values onto the [0,1] interval
            foreach(var cell in matrix.Keys.ToArray())
            {
                matrix[cell] = matrix[cell] / highest; 
            }

            // write out this matrix into Gephi format

            const string GephiFileName = "gephi-matrix.csv";

            if(File.Exists(GephiFileName))
                File.Delete(GephiFileName);

            File.AppendAllText(GephiFileName, $";{string.Join(";",activities.Select(GetNiceName))}\r\n" );

            foreach ( var activity in activities)
            {
                var vals = activities
                    .Select( a => matrix.ContainsKey($"{ activity }_{a}") ? matrix[$"{ activity }_{a}"] : 0d );

                File.AppendAllText(GephiFileName, $"{GetNiceName(activity)};{string.Join(";", vals )}\r\n");
            }

        }

        /// <summary>
        /// out of all the days, which ones was the feature in play from i.e. been used once before
        /// </summary>
        /// <param name="days"></param>
        /// <param name="data"></param>
        /// <param name="activityKey"></param>
        /// <returns></returns>
        static IEnumerable<DateTime> GetDaysInPlay(
            List<DateTime> days,
            IReadOnlyDictionary<string, Attribute> data,
            string activityKey)
        {
            return Enumerable
                .Range(0, days.Count() - days.FindIndex(d => data[activityKey].Data.ContainsKey(d)))
                .Select(i => days[i]);
        }

        /// <summary>
        /// replace any weird chars that might mess up gephi
        /// </summary>
        /// <param name="activity"></param>
        static string GetNiceName(
            string activity)
        {
            return Regex.Replace(activity, @"[^\w\d]",string.Empty);
        }

        public static string GetMoodCode(string moodString, string[] moods)
        {
            return $"{Array.FindIndex(moods, m => m == moodString)}";
        }
    }
}
