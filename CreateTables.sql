USE master
Go
IF NOT EXISTS(
	SELECT name
	FROM sys.databases
	WHERE name = N'DDDTest'
	)
CREATE DATABASE [DDDTest]
GO

USE DDDTest
GO

CREATE TABLE Users (
	UserID INT IDENTITY(1,1) PRIMARY KEY, 
	StudentNum INT,
	Name NVARCHAR(50),
	Email NVARCHAR(255),
	Password NVARCHAR(50)
);
GO

CREATE TABLE Societies (
	SocietyID INT IDENTITY(1,1) PRIMARY KEY,
	Name NVARCHAR(100)
);
GO

CREATE TABLE SocietyMembers (
	SMemberID INT IDENTITY(1,1) PRIMARY KEY,
	SocietyID INT,
	UserID INT,
	FOREIGN KEY (SocietyID) REFERENCES Societies(SocietyID),
	FOREIGN KEY (UserID) REFERENCES Users(UserID)
);
GO

CREATE TABLE Admins (
	AdminID INT IDENTITY(1,1) PRIMARY KEY, 
	UserID INT,
	SocietyID INT,
	FOREIGN KEY (UserID) REFERENCES Users(UserID),
	FOREIGN KEY (SocietyID) REFERENCES Societies(SocietyID)
);
GO

CREATE TABLE Posts (
	PostID INT IDENTITY(1,1) PRIMARY KEY,
	SocietyID INT,
	Title NVARCHAR(100),
	Text NVARCHAR(500),
	Image IMAGE,
	PostTime DATETIME,
	FOREIGN KEY (SocietyID) REFERENCES Societies(SocietyID)
);
GO

CREATE TABLE PostResponses (
	ResponseID INT IDENTITY(1,1) PRIMARY KEY,
	PostID INT,
	UserID INT,
	Text NVARCHAR(500),
	FOREIGN KEY (PostID) REFERENCES Posts(PostID),
	FOREIGN KEY (UserID) REFERENCES Users(UserID)
);
GO


CREATE TABLE Chats (
	ChatID INT IDENTITY(1,1) PRIMARY KEY,
	SocietyID INT,
	ChatName NVARCHAR(25),
	FOREIGN KEY (SocietyID) REFERENCES Societies(SocietyID)
);
GO

CREATE TABLE ChatMembers (
	CMemberID INT IDENTITY(1,1) PRIMARY KEY,
	ChatID INT,
	UserID INT,
	FOREIGN KEY (ChatID) REFERENCES Chats(ChatID),
	FOREIGN KEY (UserID) REFERENCES Users(UserID)
);
GO

CREATE TABLE  Messages (
	MessageID INT IDENTITY(1,1) PRIMARY KEY,
	ChatID INT,
	UserID INT,
	Text NVARCHAR(500),
	Image IMAGE,
	PostTime DATETIME,
	FOREIGN KEY (ChatID) REFERENCES Chats(ChatID),
	FOREIGN KEY (UserID) REFERENCES Users(UserID)
);

GO