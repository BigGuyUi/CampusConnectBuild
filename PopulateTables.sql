USE DDDTest;
GO

SET NOCOUNT ON;

BEGIN TRAN;

INSERT INTO Users (StudentNum, Name, Email, Password) VALUES
(100000001,'Alice Johnson','a.johnson-2023@hull.ac.uk','pass1'),
(100000002,'Ben Smith','b.smith-2024@hull.ac.uk','pass2'),
(100000003,'Chloe Williams','c.williams-2022@hull.ac.uk','pass3'),
(100000004,'Daniel Brown','d.brown-2025@hull.ac.uk','pass4'),
(100000005,'Ella Jones','e.jones-2023@hull.ac.uk','pass5'),
(100000006,'Finn Miller','f.miller-2021@hull.ac.uk','pass6'),
(100000007,'Grace Davis','g.davis-2024@hull.ac.uk','pass7'),
(100000008,'Harry Wilson','h.wilson-2025@hull.ac.uk','pass8');

INSERT INTO Societies (Name) VALUES
('Computer Science Society'),
('Drama Society'),
('Gaming Society'),
('Hiking Society'),
('Photography Society');

INSERT INTO SocietyMembers (SocietyID, UserID) VALUES
(1,1),(1,2),(1,3),
(2,3),(2,4),(2,5),
(3,1),(3,4),(3,6),
(4,2),(4,5),(4,7),
(5,6),(5,7),(5,8);

INSERT INTO Admins (UserID, SocietyID) VALUES
(1,1),
(3,2),
(4,3),
(2,4),
(6,5);

INSERT INTO Posts (SocietyID, Title, Text, Image, PostTime) VALUES
(1,'Welcome','Welcome to CS Society!',NULL,GETDATE()-3),
(1,'Event','CS coding night this Friday!',NULL,GETDATE()-1),
(2,'Auditions','Drama auditions open!',NULL,GETDATE()-2),
(2,'Workshop','Acting workshop on Saturday.',NULL,GETDATE()-1),
(3,'Tournament','Gaming 1v1 tournament.',NULL,GETDATE()-4),
(3,'LAN Party','LAN party planned soon.',NULL,GETDATE()-1),
(4,'Hike','Weekend hiking trip!',NULL,GETDATE()-5),
(4,'Reminder','Bring boots.',NULL,GETDATE()-1),
(5,'Photo Walk','Photography walk this week.',NULL,GETDATE()-3),
(5,'Competition','Submit your best shots!',NULL,GETDATE()-1);

INSERT INTO PostResponses (ResponseID, PostID, UserID, Text) VALUES
(DEFAULT,1,2,'Sounds great!'),
(DEFAULT,1,3,'I will be there.'),
(DEFAULT,2,1,'Awesome!'),
(DEFAULT,2,3,'Can’t wait.'),
(DEFAULT,3,4,'Exciting.'),
(DEFAULT,3,5,'Count me in.'),
(DEFAULT,4,5,'Nice!'),
(DEFAULT,4,3,'I''m joining.'),
(DEFAULT,5,1,'Cool tournament.'),
(DEFAULT,5,6,'Ready!'),
(DEFAULT,6,4,'LAN!!!!!'),
(DEFAULT,6,1,'Let’s go.'),
(DEFAULT,7,2,'Fun hike!'),
(DEFAULT,7,7,'Joining!'),
(DEFAULT,8,5,'I have boots.'),
(DEFAULT,8,7,'All good.'),
(DEFAULT,9,6,'Great walk.'),
(DEFAULT,9,7,'Taking photos.'),
(DEFAULT,10,8,'I''m entering!'),
(DEFAULT,10,6,'Good luck everyone.');

INSERT INTO Chats (SocietyID, ChatName) VALUES
(1,'cs-general'),
(2,'drama-general'),
(3,'gaming-general'),
(4,'hiking-general'),
(5,'photo-general');

INSERT INTO ChatMembers (ChatID, UserID)
SELECT c.ChatID, sm.UserID
FROM Chats c
JOIN SocietyMembers sm ON sm.SocietyID = c.SocietyID;

INSERT INTO Messages (ChatID, UserID, Text, Image, PostTime) VALUES
(1,1,'Hello CS members!',NULL,GETDATE()-1),
(1,2,'Hi everyone!',NULL,GETDATE()),
(2,3,'Drama chat opened.',NULL,GETDATE()-1),
(2,4,'Looking forward to shows.',NULL,GETDATE()),
(3,1,'Gaming chat on.',NULL,GETDATE()-1),
(3,6,'Ready for games!',NULL,GETDATE()),
(4,2,'Hiking chat.',NULL,GETDATE()-1),
(4,7,'Let’s climb!',NULL,GETDATE()),
(5,6,'Photo chat active.',NULL,GETDATE()-1),
(5,8,'Taking pics!',NULL,GETDATE());

COMMIT TRAN;
GO