using System;
<<<<<<< HEAD
using System.IO;
=======
>>>>>>> ccf0a8e... Basic command recognition, reaction handling, poll creation
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gabot.Modules
{
<<<<<<< HEAD
    [Serializable]
=======
>>>>>>> ccf0a8e... Basic command recognition, reaction handling, poll creation
    public class Poll
    {
        public DateTime startDate = new DateTime();
        public DateTime endDate = new DateTime();
        public string title;
        public List<Movie> movieList = new List<Movie>();
<<<<<<< HEAD
        public List<string> messageIDs = new List<string>();
=======
>>>>>>> ccf0a8e... Basic command recognition, reaction handling, poll creation
    }
}
