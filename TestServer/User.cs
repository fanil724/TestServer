namespace TestServer
{
    public class User
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Surname { get; set; } //фамилия
        public string? SecondName { get; set; } // отчетсво
        public string? Department { get; set; }
        public string? Position { get; set; }
        public string? Login { get; set; }
        public string? Password { get; set; }
    }

    public class ScoreTable
    {
        public int Id { get; set; }
        public User? User { get; set; }
        public int UserId { get; set; }
        public Theme? Theme { get; set; }
        public int ThemeId { get; set; }
        public int Score { get; set; }
    }

    public class Help
    {
        public int Id { get; set; }
        public string Text { get; set; }
    }
}
