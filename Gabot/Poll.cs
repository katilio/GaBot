using System;
using Discord.Rest;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;

namespace Gabot.Modules
{
    [Serializable]
    public class Poll : IEnumerable,IEnumerator
    {
        public DateTime startDate = new DateTime();
        public DateTime endDate = new DateTime();
        public string title;
        public List<Movie> movieList = new List<Movie>();
        public List<ulong> messageIDs = new List<ulong>();
        int position = -1;
        [NonSerialized]
        public RestUserMessage lastMessage;


        public IEnumerator GetEnumerator()
        {
            return (IEnumerator)this;
        }

        //IEnumerator
        public bool MoveNext()
        {
            position++;
            return (position < movieList.Count());
        }

        //IEnumerable
        public void Reset()
        { position = 0; }

        //IEnumerable
        public object Current
        {
            get { return movieList[position]; }
        }
    }
}
