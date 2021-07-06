﻿using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;


namespace Server
{
    internal static class Program
    {
        private static void Main()
        {
            ShowInfo("Сервер запущен");
            var server = new TCPServer("127.0.0.1", 8005);
            server.Start();
            ShowInfo("Ожидаю подключение...");
            while (true)
            {

                var newTCPClient = server.NewClient();
                string nickname = "login";
                string password = "pass";
                string option = "1";
                var newClient =  new User
                {
                    nickname = nickname,
                    password = password,
                    tcpclient = newTCPClient
                };
                Console.WriteLine($"Клиент {newClient.nickname} {DateTime.Now:u}: Клиент подключился.");
                
                 try
                 {
                     RequestHandler(server, newClient, option);
                 }
                 catch (Exception e)
                 {
                     server.SendMessageToClient(newClient.nickname, new Message
                     {
                         Date = $"{DateTime.Now:u}",
                         Msg = $"Выход из программы."
                     });
                     newTCPClient.Close();
                     Console.WriteLine($"Клиент {newClient.nickname} {DateTime.Now:u}: Клиент отключился.");
                     break;
                 }

                 var task = Task.Run(() => MessageHandler(server));
            }
        }


        static bool RequestHandler(TCPServer server, User newClient, string option)
        {
            var db_api = new DB_api();
            db_api.Connect();
            User sender = new User();
            User receiver = new User();

            switch (option)
            {
                case "1":
                    try
                    {
                        Registration(server, newClient, db_api);
                    }
                    catch (Exception)
                    {
                        throw new Exception();
                    }
                    break;

                case "2":
                    try
                    {
                        Authorization(server, newClient, db_api);
                    }
                    catch (Exception)
                    {
                        throw new Exception();
                    }
                    break;

                case "3":
                    MessageHandler(server);
                    break;

                case "4":
                    //Add Chat
                    // выполнено в insertMessage
                case "5":
                    DropTheChat(sender, receiver, db_api);
                    break;

                case "6":
                    List<Message> chat = ReturnChat(sender.nickname, receiver.nickname, db_api);
                    var response = new Request<List<Message>>(chat, "3");
                    server.SendMessageToClient(sender.nickname, response);
                    break;

                case "7":
                    DropTheChat(sender, receiver, db_api, server);

                    break;

                case "9":
                    //Return Contact list;
                    break;

                case "10":
                    //Add new Contact
                    break;

                case "11":
                    //delete contact
                    //drop chat
                    break;
                    
                case "12":
                    ClientDisconnect(sender.nickname, receiver.nickname, newClient, server);
                    break;

                case "13":
                    return false;

                default:
                    break;
            }

            return true;
        }


        static void Registration(TCPServer server, User newClient, ref DB_api db_api)
        {
            if (db_api.Registration(newClient.nickname, newClient.password))
            {
                server.SendMessageToClient(newClient.nickname, new Message
                {
                    Date = $"{DateTime.Now:u}",
                    Msg = "Регистрация завершена успешно."

                });
                // Журналирование
            }
            else
            {
                server.SendMessageToClient(newClient.nickname, new Message
                {
                    Date = $"{DateTime.Now:u}",
                    Msg = $"Отказ регистрации. Никнейм {newClient.nickname} уже занят."
                });
                Console.WriteLine(
                    $"Клиент {newClient.nickname} {DateTime.Now:u}: Отказ регистрации. Попытка повторной регистрации.");
                // Журналирование
                throw new Exception();
            }
        }

        static void Authorization(TCPServer server, User newClient, ref DB_api db_api)
        {
            if (db_api.Authentication(newClient.nickname, newClient.password))
            {
                server.SendMessageToClient(newClient.nickname, new Message
                {
                    Date = $"{DateTime.Now:u}",
                    Msg = "Регистрация завершена успешно."

                });
                Console.WriteLine($"Клиент {newClient.nickname} {DateTime.Now:u}: Успешная авторизация.");

                //Журналирование успех
            }
            else
            {
                server.SendMessageToClient(newClient.nickname, new Message
                {
                    Date = $"{DateTime.Now:u}",
                    Msg = $"Отказ авторизации. Неправельный никнейм или пароль."
                });
                Console.WriteLine(
                    $"Клиент {newClient.nickname} {DateTime.Now:u}: Отказ авторизации. Неправельный никнейм или пароль.");
                // Журналирование
                throw new ArgumentException();
            }
        }
        
        static void ClientDisconnect(string sender, string receiver, User client, TCPServer server)
        {
            server.DeleteClient(client.nickname);

            client.tcpclient.Close();
            Console.WriteLine($"Клиент {sender} {DateTime.Now:u}: Клиент отключился.");
            // Добаление записи в журнал
        }

        static void DropTheChat(string sender, string receiver, DB_API db_api, TCPServer server)
        {

            db_api.Drop(sender, receiver);
            var msg = new Message
            {
                Date = $"{DateTime.Now:u}",
                Msg = $"Беседа с {receiver} удалена."
            };
            server.SendMessageToClient(sender, msg);
            Console.WriteLine($"Клиент {sender} {DateTime.Now:u}: Удаление беседы {sender}-{receiver}.");
            // журналирование
        }

        static List<Message> ReturnChat (string sender, string receiver, DB_API db_api)
        {
            List<Message> chat = new List<Message>();
            var msgList = db_api.GetMsgList(sender, receiver);
            foreach (var msg in msgList)
            {
                var msgObject = new Message
                {
                    SenderNickname = sender,
                    ReceiverNickname = receiver,
                    Date = $"{DateTime.Now:u}",
                    Msg = msg
                };

                chat.Add(msgObject);
            }
            return chat;
        }

        static void MessageHandler(TCPServer server)
        {
            while (true)
            {
                var msg_temp = JsonSerializer.Deserialize<Message>(server.GetMessage());
                var sender_name = msg_temp.SenderNickname;
                var receiver_name = msg_temp.ReceiverNickname;

                ShowInfo($"{sender_name} to  {receiver_name} : {msg_temp.Msg}");
                server.SendMessageToClient(sender_name, new Message
                {
                    Date = $"{DateTime.Now:u}",
                    Msg = $"Вы {msg_temp.Date}: {msg_temp.Msg}"
                });

                server.SendMessageToClient(receiver_name, new Message
                {
                    Date = $"{DateTime.Now:u}",
                    Msg = $"{sender_name} {msg_temp.Date}: {msg_temp.Msg}"
                });
            }
        }

        static public List<string> GiveMeContactList(User client, TCPServer server, DB_api db_api)
        {
           //Журналирование 
           return db_api.GetContactList(client.nickname);
        }

        static void ShowInfo(string message)
        {
            Console.ForegroundColor = ConsoleColor.DarkBlue;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        static List<string> GiveMeMassegeList(User sender, User receiver, DB_api db_api)
        {
            var msgList = db_api.GetMsgList(sender.nickname, receiver.nickname);
            
            

            return db_api.GetMsgList(sender.nickname, receiver.nickname);
        }

        static string MessageTypeMessage(string message)
        {
            var msg = new Message
            {
                //Данные класса
            };
            var msg_send = JsonSerializer.Serialize(msg);
            return msg_send;
        }

        
            

    }
}