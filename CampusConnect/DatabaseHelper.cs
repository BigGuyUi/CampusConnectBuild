using System;
using System.Data.SQLite;
using System.IO;

namespace CampusConnect
{
    public class DatabaseHelper
    {
        public static void IntialiseDatabase(string connectionstring_)
        {
            // Parse the connection string to get the Data Source and make it absolute
            var builder = new SQLiteConnectionStringBuilder(connectionstring_);
            var dataSource = builder.DataSource ?? "CampusConnect.db";

            if (!Path.IsPathRooted(dataSource))
            {
                // Use the current directory (app's working directory) as base
                dataSource = Path.Combine(Directory.GetCurrentDirectory(), dataSource);
            }

            // Ensure directory exists
            var dir = Path.GetDirectoryName(dataSource);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // If file doesn't exist, create it
            if (!File.Exists(dataSource))
            {
                SQLiteConnection.CreateFile(dataSource);
            }

            // Ensure the connection string uses the absolute path
            builder.DataSource = dataSource;
            var absoluteConnectionString = builder.ToString();

            using (var connection = new SQLiteConnection(absoluteConnectionString))
            {
                connection.Open();

                // Ensure foreign key enforcement
                using (var pragma = new SQLiteCommand("PRAGMA foreign_keys = ON;", connection))
                {
                    pragma.ExecuteNonQuery();
                }

                using (var transaction = connection.BeginTransaction())
                using (var cmd = connection.CreateCommand())
                {
                    // Create tables (SQLite compatible)
                    var createSql = @"
CREATE TABLE IF NOT EXISTS Users (
    UserID INTEGER PRIMARY KEY AUTOINCREMENT,
    StudentNum INTEGER,
    Name TEXT,
    Email TEXT,
    Password TEXT
);

CREATE TABLE IF NOT EXISTS Societies (
    SocietyID INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT
);

CREATE TABLE IF NOT EXISTS SocietyMembers (
    SMemberID INTEGER PRIMARY KEY AUTOINCREMENT,
    SocietyID INTEGER,
    UserID INTEGER,
    FOREIGN KEY (SocietyID) REFERENCES Societies(SocietyID),
    FOREIGN KEY (UserID) REFERENCES Users(UserID)
);

CREATE TABLE IF NOT EXISTS Admins (
    AdminID INTEGER PRIMARY KEY AUTOINCREMENT,
    UserID INTEGER,
    SocietyID INTEGER,
    FOREIGN KEY (UserID) REFERENCES Users(UserID),
    FOREIGN KEY (SocietyID) REFERENCES Societies(SocietyID)
);

CREATE TABLE IF NOT EXISTS Posts (
    PostID INTEGER PRIMARY KEY AUTOINCREMENT,
    SocietyID INTEGER,
    Title TEXT,
    Text TEXT,
    Image BLOB,
    PostTime TEXT,
    FOREIGN KEY (SocietyID) REFERENCES Societies(SocietyID)
);

CREATE TABLE IF NOT EXISTS PostResponses (
    ResponseID INTEGER PRIMARY KEY AUTOINCREMENT,
    PostID INTEGER,
    UserID INTEGER,
    Text TEXT,
    FOREIGN KEY (PostID) REFERENCES Posts(PostID),
    FOREIGN KEY (UserID) REFERENCES Users(UserID)
);

CREATE TABLE IF NOT EXISTS Chats (
    ChatID INTEGER PRIMARY KEY AUTOINCREMENT,
    SocietyID INTEGER,
    ChatName TEXT,
    FOREIGN KEY (SocietyID) REFERENCES Societies(SocietyID)
);

CREATE TABLE IF NOT EXISTS ChatMembers (
    CMemberID INTEGER PRIMARY KEY AUTOINCREMENT,
    ChatID INTEGER,
    UserID INTEGER,
    FOREIGN KEY (ChatID) REFERENCES Chats(ChatID),
    FOREIGN KEY (UserID) REFERENCES Users(UserID)
);

CREATE TABLE IF NOT EXISTS Messages (
    MessageID INTEGER PRIMARY KEY AUTOINCREMENT,
    ChatID INTEGER,
    UserID INTEGER,
    Text TEXT,
    Image BLOB,
    PostTime TEXT,
    FOREIGN KEY (ChatID) REFERENCES Chats(ChatID),
    FOREIGN KEY (UserID) REFERENCES Users(UserID)
);
";
                    // Execute each statement separately to avoid multi-statement quirks
                    foreach (var statement in createSql.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var s = statement.Trim();
                        if (string.IsNullOrWhiteSpace(s))
                            continue;

                        cmd.CommandText = s + ";";
                        cmd.ExecuteNonQuery();
                    }

                    // Seed data only if Users table is empty
                    cmd.CommandText = "SELECT COUNT(1) FROM Users;";
                    var userCount = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                    if (userCount == 0)
                    {
                        // Insert users
                        cmd.CommandText = @"
INSERT INTO Users (StudentNum, Name, Email, Password) VALUES
(100000001,'Alice Johnson','a.johnson-2023@hull.ac.uk','pass1'),
(100000002,'Ben Smith','b.smith-2024@hull.ac.uk','pass2'),
(100000003,'Chloe Williams','c.williams-2022@hull.ac.uk','pass3'),
(100000004,'Daniel Brown','d.brown-2025@hull.ac.uk','pass4'),
(100000005,'Ella Jones','e.jones-2023@hull.ac.uk','pass5'),
(100000006,'Finn Miller','f.miller-2021@hull.ac.uk','pass6'),
(100000007,'Grace Davis','g.davis-2024@hull.ac.uk','pass7'),
(100000008,'Harry Wilson','h.wilson-2025@hull.ac.uk','pass8');
";
                        cmd.ExecuteNonQuery();

                        // Societies
                        cmd.CommandText = @"
INSERT INTO Societies (Name) VALUES
('Computer Science Society'),
('Drama Society'),
('Gaming Society'),
('Hiking Society'),
('Photography Society');
";
                        cmd.ExecuteNonQuery();

                        // SocietyMembers
                        cmd.CommandText = @"
INSERT INTO SocietyMembers (SocietyID, UserID) VALUES
(1,1),(1,2),(1,3),
(2,3),(2,4),(2,5),
(3,1),(3,4),(3,6),
(4,2),(4,5),(4,7),
(5,6),(5,7),(5,8);
";
                        cmd.ExecuteNonQuery();

                        // Admins
                        cmd.CommandText = @"
INSERT INTO Admins (UserID, SocietyID) VALUES
(1,1),
(3,2),
(4,3),
(2,4),
(6,5);
";
                        cmd.ExecuteNonQuery();

                        // Posts - using SQLite datetime expressions to simulate GETDATE() - N days
                        cmd.CommandText = @"
INSERT INTO Posts (SocietyID, Title, Text, Image, PostTime) VALUES
(1,'Welcome','Welcome to CS Society!',NULL, datetime('now','-3 days')),
(1,'Event','CS coding night this Friday!',NULL, datetime('now','-1 days')),
(2,'Auditions','Drama auditions open!',NULL, datetime('now','-2 days')),
(2,'Workshop','Acting workshop on Saturday.',NULL, datetime('now','-1 days')),
(3,'Tournament','Gaming 1v1 tournament.',NULL, datetime('now','-4 days')),
(3,'LAN Party','LAN party planned soon.',NULL, datetime('now','-1 days')),
(4,'Hike','Weekend hiking trip!',NULL, datetime('now','-5 days')),
(4,'Reminder','Bring boots.',NULL, datetime('now','-1 days')),
(5,'Photo Walk','Photography walk this week.',NULL, datetime('now','-3 days')),
(5,'Competition','Submit your best shots!',NULL, datetime('now','-1 days'));
";
                        cmd.ExecuteNonQuery();

                        // PostResponses
                        cmd.CommandText = @"
INSERT INTO PostResponses (PostID, UserID, Text) VALUES
(1,2,'Sounds great!'),
(1,3,'I will be there.'),
(2,1,'Awesome!'),
(2,3,'Can''t wait.'),
(3,4,'Exciting.'),
(3,5,'Count me in.'),
(4,5,'Nice!'),
(4,3,'I''m joining.'),
(5,1,'Cool tournament.'),
(5,6,'Ready!'),
(6,4,'LAN!!!!!'),
(6,1,'Let''s go.'),
(7,2,'Fun hike!'),
(7,7,'Joining!'),
(8,5,'I have boots.'),
(8,7,'All good.'),
(9,6,'Great walk.'),
(9,7,'Taking photos.'),
(10,8,'I''m entering!'),
(10,6,'Good luck everyone.');
";
                        cmd.ExecuteNonQuery();

                        // Chats
                        cmd.CommandText = @"
INSERT INTO Chats (SocietyID, ChatName) VALUES
(1,'cs-general'),
(2,'drama-general'),
(3,'gaming-general'),
(4,'hiking-general'),
(5,'photo-general');
";
                        cmd.ExecuteNonQuery();

                        // ChatMembers - insert from JOIN (supported in SQLite)
                        cmd.CommandText = @"
INSERT INTO ChatMembers (ChatID, UserID)
SELECT c.ChatID, sm.UserID
FROM Chats c
JOIN SocietyMembers sm ON sm.SocietyID = c.SocietyID;
";
                        cmd.ExecuteNonQuery();

                        // Messages
                        cmd.CommandText = @"
INSERT INTO Messages (ChatID, UserID, Text, Image, PostTime) VALUES
(1,1,'Hello CS members!',NULL, datetime('now','-1 days')),
(1,2,'Hi everyone!',NULL, datetime('now')),
(2,3,'Drama chat opened.',NULL, datetime('now','-1 days')),
(2,4,'Looking forward to shows.',NULL, datetime('now')),
(3,1,'Gaming chat on.',NULL, datetime('now','-1 days')),
(3,6,'Ready for games!',NULL, datetime('now')),
(4,2,'Hiking chat.',NULL, datetime('now','-1 days')),
(4,7,'Let''s climb!',NULL, datetime('now')),
(5,6,'Photo chat active.',NULL, datetime('now','-1 days')),
(5,8,'Taking pics!',NULL, datetime('now'));
";
                        cmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }
        }
    }
}
