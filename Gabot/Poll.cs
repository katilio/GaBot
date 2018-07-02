using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gabot.Modules
{
    public class Poll
    {
        public DateTime startDate = new DateTime();
        public DateTime endDate = new DateTime();
        public string title;
        public List<Movie> movieList = new List<Movie>();
    }
}
