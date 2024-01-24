namespace TestServer
{
    public class Question
    {
        public int Id { get; set; }
        public string Ask { get; set; }
        public string Answer1 { get; set; }
        public string Answer2 { get; set; }
        public string Answer3 { get; set; }
        public string Answer4 { get; set; }
        public int rightAnswer { get; set; }
        public int themeId { get; set; }
        public Theme? theme { get; set; }

    }

    public class Theme
    {
        public int Id { get; set; }
        public string ThemeName { get; set; }
        public bool IsHidden { get; set; }
    }
}
