namespace SuitLog
{
    public class ListItem
    {
        public string id;
        public string text;
        public bool rumored;
        public int indentation;
        public bool unread;
        public bool moreToExplore;
        public bool markedOnHUD;

        public ListItem(string id, string text, bool rumored, int indentation, bool unread, bool moreToExplore, bool markedOnHUD)
        {
            this.id = id;
            this.text = text;
            this.rumored = rumored;
            this.indentation = indentation;
            this.unread = unread;
            this.moreToExplore = moreToExplore;
            this.markedOnHUD = markedOnHUD;
        }
    }
}