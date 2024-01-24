using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using TestServer;

ServerObject server = new ServerObject();
await server.ListenAsync();

class ServerObject
{
    TcpListener tcplistener = new TcpListener(IPAddress.Loopback, 8888);
    List<ClientObject> clients = new List<ClientObject>();

    protected internal void RemoveConnetion(string id)
    {
        ClientObject? client = clients.FirstOrDefault(x => x.Id == id);
        if (client != null) clients.Remove(client);
        client?.Close();
    }
    protected internal async Task ListenAsync()
    {
        try
        {
            tcplistener.Start();
            Console.WriteLine("Сервер запущен, Ожидайте подключение....");
            while (true)
            {
                TcpClient tcpClient = await tcplistener.AcceptTcpClientAsync();
                ClientObject client = new ClientObject(tcpClient, this);
                clients.Add(client);
                Task.Run(client.ProcessAsync);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
        finally { Disconnect(); }
    }

    protected internal void Disconnect()
    {
        foreach (ClientObject client in clients)
        {
            client.Close();
        }
        tcplistener.Stop();
    }
}

class ClientObject
{
    protected internal string Id { get; } = Guid.NewGuid().ToString();
    protected internal StreamWriter Writer { get; }
    protected internal StreamReader Reader { get; }

    TcpClient client;
    ServerObject serverObject;

    public ClientObject(TcpClient tcpClient, ServerObject server)
    {
        client = tcpClient;
        serverObject = server;
        var stream = client.GetStream();
        Writer = new StreamWriter(stream);
        Reader = new StreamReader(stream);
    }

    public async Task ProcessAsync()
    {
        User? user = new User();
        string? str;
        try
        {

            str = await Reader.ReadLineAsync();
            if (str == string.Empty) return;
            var userLogAndPass = str!.Split(":::");

            if (userLogAndPass[0] == "Admin")
            {
                char[] chars;
                using (FileStream fs = new FileStream("Admin.dat", FileMode.Open, FileAccess.Read))
                {
                    byte[] bytes = new byte[(int)fs.Length];
                    fs.Read(bytes, 0, bytes.Length);
                    chars = Encoding.Default.GetChars(bytes);
                    for (int i = 0; i < chars.Length; i++) chars[i] = (char)((int)chars[i] + 32);
                }
                string password = new string(chars);
                if (password != userLogAndPass[1])
                {
                    await Writer.WriteLineAsync("Пароль не верный!"); return;
                }
                else
                {
                    await Writer.WriteLineAsync("admin");
                    user.Name = "админ";
                }
            }
            else
            {
                using (ApplicationContext db = new ApplicationContext())
                {
                    user = db.Users.FirstOrDefault(n => n.Login == userLogAndPass[0]);
                }

                if (user == null)
                {
                    await Writer.WriteLineAsync("Пользователь с такой учетной записью не обнаружен"); return;
                }
                else
                {
                    if (user.Password != userLogAndPass[1])
                    {
                        await Writer.WriteLineAsync("Пароль не правильный"); return;
                    }
                    else
                    {
                        string jsonUser = JsonSerializer.Serialize(user);
                        await Writer.WriteLineAsync(jsonUser);
                    }
                }
            }


            Console.WriteLine($"{user.Name} Подключился к серверу!");
            while (true)
            {
                try
                {
                    str = await Reader.ReadLineAsync();
                    str = RequestHandler(str);
                    if (str!="") { await Writer.WriteLineAsync(str); }
                }
                catch
                {
                    Console.WriteLine($"{user.Name} Отключился от сервера!");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        finally
        {
            serverObject.RemoveConnetion(Id);
        }
    }

    protected internal void Close()
    {
        client?.Close();
        Writer?.Close();
        Reader?.Close();
    }

    private string RequestHandler(string str)
    {

        string message = "";
        var request = str.Split(":::");
        switch (request[0])
        {
            case "Get":
                {
                    if (request[1] == "UserList")// Получение списка пользователей
                    {
                        using (ApplicationContext db = new ApplicationContext())
                        {
                            var userlist = db.Users.ToList();
                            message = JsonSerializer.Serialize(userlist);
                        }
                    }

                    if (request[1] == "ThemeList") //Получение списка тем 
                    {
                        using (ApplicationContext db = new ApplicationContext())
                        {
                            var themelist = db.Themes.ToList();
                            message = JsonSerializer.Serialize(themelist);
                        }
                    }

                    if (request[1] == "QuestionList")//Получение списка вопросов по теме 
                    {
                        using (ApplicationContext db = new ApplicationContext())
                        {
                            var questionlist = db.Questions.Include(x => x.theme).Where(x => x.theme.ThemeName == request[2]).ToList();
                            message = JsonSerializer.Serialize(questionlist);
                        }
                    }

                    if (request[1] == "Statistics")//Запрос  статистики
                    {
                        using (ApplicationContext db = new ApplicationContext())
                        {
                            List<ScoreTable> statisticlist;
                            if (request.Length == 3)
                            {
                                statisticlist = db.ScoreTables.Include(x => x.User).Where(x => x.UserId == Int32.Parse(request[2])).ToList();
                            }
                            else
                            {

                                statisticlist = db.ScoreTables.ToList();
                            }
                            message = JsonSerializer.Serialize(statisticlist);
                        }
                    }

                    if (request[1] == "Test")// Запрос теста по теме
                    {
                        List<Question> questions = new List<Question>();
                        int countQuestions = 10; //количество вопросв в тесте
                        using (ApplicationContext db = new ApplicationContext())
                        {
                            questions = db.Questions.Include(x => x.theme).Where(x => x.theme.ThemeName == request[2]).ToList();
                        }

                        if (questions.Count < countQuestions)
                        {
                            message = "В базе вопросов меньше чем надо для проведения тестирования. Обратитесь к администратору.";
                        }
                        else
                        {
                            var Test = new List<Question>();
                            Random r = new Random();
                            int k = 0, temp;
                            while (k < countQuestions) //сборка 10 неповторяющихя случайных вопросов из существующего списка вопросов
                            {
                                temp = r.Next(questions.Count);
                                bool check = true;
                                for (int i = 0; i < k; i++)
                                {
                                    if (Test[i].Id == questions[temp].Id)
                                    { check = false; }
                                }
                                if (check)
                                {
                                    Test.Add(questions[temp]);
                                    k++;
                                }
                            }
                            message = JsonSerializer.Serialize(Test);
                        }
                    }
                }
                break;

            case "Set":
                {
                    if (request[1] == "User")// Возврат на сервер пользователя 
                    {
                        User? user = JsonSerializer.Deserialize<User>(request[2]);
                        using (ApplicationContext db = new ApplicationContext())
                        {
                            var us = db.Users.FirstOrDefault(x => x.Id == user.Id);
                            if (us == null)
                            {
                                db.Users.Add(user);
                            }
                            else
                            {
                                db.Users.Update(user);
                            }
                            db.SaveChanges();
                        }
                    }

                    if (request[1] == "Theme")// Возврат на сервер темы
                    {
                        Theme? th = JsonSerializer.Deserialize<Theme>(request[2]);
                        using (ApplicationContext db = new ApplicationContext())
                        {
                            var us = db.Themes.FirstOrDefault(x => x.Id == th.Id);
                            if (us == null)
                            {
                                db.Themes.Add(th);
                            }
                            else
                            {
                                db.Themes.Update(th);
                            }
                            db.SaveChanges();
                        }
                    }

                    if (request[1] == "Question")// Возврат на сервер вопроса 
                    {
                        Question? question = JsonSerializer.Deserialize<Question>(request[2]);
                        using (ApplicationContext db = new ApplicationContext())
                        {
                            var us = db.Questions.FirstOrDefault(x => x.Id == question.Id);
                            if (us == null)
                            {
                                db.Questions.Add(question);
                            }
                            else
                            {
                                db.Questions.Update(question);
                            }
                            db.SaveChanges();

                        }
                    }
                    if (request[1] == "Del")// удаление пользователя 
                    {
                        User? user = JsonSerializer.Deserialize<User>(request[2]);
                        using (ApplicationContext db = new ApplicationContext())
                        {
                            var us = db.Users.FirstOrDefault(x => x.Id == user.Id);
                            if (us != null)
                            {
                                db.Users.Remove(user);
                                db.SaveChanges();
                            }

                        }
                    }
                }
                break;
        }
        return message;
    }
}