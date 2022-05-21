namespace SuitLog
{
    public class ListItem
    {
        public string id;
        public string text;
        public bool rumored;
        public bool indented;
        public bool unread;
        public bool moreToExplore;
        public bool markedOnHUD;

        public ListItem(string id, string text, bool rumored, bool indented, bool unread, bool moreToExplore, bool markedOnHUD)
        {
            this.id = id;
            this.text = text;
            this.rumored = rumored;
            this.indented = indented;
            this.unread = unread;
            this.moreToExplore = moreToExplore;
            this.markedOnHUD = markedOnHUD;
        }
    }
}