﻿using System;
using System.Data.SqlClient;
using DevSpace.Common;

namespace DevSpace.Database {
	public class SqlDatabase : IDatabase {
		private SqlConnection Connection;

		private bool ConnectToMaster() {
			try {
				SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder( Settings.ConnectionString );
				builder.InitialCatalog = "master";
				builder.ConnectTimeout = 300; // We have to give enough time for the database to be created

				Connection = new SqlConnection( builder.ConnectionString );
				Connection.Open();
				return true;
			} catch( Exception Ex ) {
				return false;
			}
		}

		private bool CreateVersionInfo() {
			SqlCommand Command = new SqlCommand();
			Command.Connection = Connection;
			Command.CommandText =
@"CREATE TABLE VersionInfo (
DbVersion	VARCHAR(16)	NOT NULL,

CONSTRAINT VersionInfo_PK PRIMARY KEY ( DbVersion )
);

INSERT VersionInfo ( DbVersion ) VALUES ( '00.00.00.0000' );";
			Command.ExecuteNonQuery();
			return true;
		}

		private bool ConnectToDb() {
			Connection = new SqlConnection( Settings.ConnectionString );
			Connection.Open();
			return true;
		}

		private bool DbVersionTableExists() {
			SqlCommand Command = new SqlCommand();
			Command.Connection = Connection;
			Command.CommandText =
@"IF(
	EXISTS(
		SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND  TABLE_NAME = 'VersionInfo'
	)
)
	SELECT CAST( 1 AS BIT );
ELSE
	SELECT CAST( 0 AS BIT );";
			return (bool)(Command.ExecuteScalar() ?? false);
		}

		private string GetDatabaseVersion() {
			if( DbVersionTableExists() ) {
				SqlCommand Command = new SqlCommand();
				Command.Connection = Connection;
				Command.CommandText = "SELECT DbVersion FROM VersionInfo;";
				return Command.ExecuteScalar()?.ToString();
			} else {
				if( CreateVersionInfo() ) {
					return "00.00.00.0000";
				} else {
					throw new Exception( "Could not create VersionInfo table" );
				}
			}
		}

		private string GetUpgradeScript( string DatabaseVersion ) {
			switch( DatabaseVersion ) {
				case "": // Because string.Empty is not a constant...
				case "00.00.00.0000":
					return
@"CREATE TABLE SponsorLevels (
	Id					INT			IDENTITY(1,1)	NOT NULL,
	DisplayOrder		INT							NOT NULL,
	DisplayName			VARCHAR(16)					NOT NULL,
	DisplayInEmails		BIT							NOT NULL,
	DisplayInSidebar	BIT							NOT NULL,

	CONSTRAINT SponsorLevels_PK PRIMARY KEY NONCLUSTERED ( Id ),
	CONSTRAINT SponsorLevels_CI UNIQUE CLUSTERED ( DisplayOrder )
);

CREATE TABLE Sponsors (
	Id				INT				IDENTITY(1,1)	NOT NULL,
	DisplayName		VARCHAR(16)						NOT NULL,
	Level			INT								NOT NULL,
	LogoSmall		VARCHAR(64)						NOT NULL,
	LogoLarge		VARCHAR(64)						NOT NULL,
	Website			VARCHAR(64)						NOT NULL,

	CONSTRAINT Sponsor_PK PRIMARY KEY ( Id ),
	CONSTRAINT Sponsors_SponsorLevels_FK FOREIGN KEY ( Level ) REFERENCES SponsorLevels ( Id )
);

UPDATE VersionInfo SET DbVersion = '01.00.00.0000';";

				case "01.00.00.0000":
					return
@"CREATE TABLE StudentCodes (
	Id					INT			IDENTITY(1,1)	NOT NULL,
	Email				VARCHAR(64)					NOT NULL,
	Code				VARCHAR(16)					NOT NULL,

	CONSTRAINT StudentCodes_PK PRIMARY KEY ( Id ),
	CONSTRAINT StudentCodes_UI UNIQUE NONCLUSTERED ( Email )
);

UPDATE VersionInfo SET DbVersion = '01.00.01.0000';";

				case "01.00.01.0000":
					return
@"CREATE TABLE Users (
	Id		INTEGER			IDENTITY(1,1)	NOT NULL,
	EmailAddress	VARCHAR(100)				NOT NULL,
	DisplayName	VARCHAR(46)				NOT NULL,
	PasswordHash	VARCHAR(128)				NOT NULL,
	Bio		VARCHAR(MAX)				NULL,
	Twitter		VARCHAR(15)				NULL,
	Website		VARCHAR(230)				NULL,
	Permissions	TINYINT				NULL,
	SessionToken	UNIQUEIDENTIFIER			NULL,
	SessionExpires	DATETIME				NULL,

	CONSTRAINT Speakers_PK PRIMARY KEY CLUSTERED ( Id ),
	CONSTRAINT Speakers_IX UNIQUE NONCLUSTERED ( EmailAddress )
);

UPDATE VersionInfo SET DbVersion = '01.00.01.0001';";

				case "01.00.01.0001":
					return
@"CREATE TABLE Tags (
	Id		INTEGER			IDENTITY(1,1)	NOT NULL,
	Text		VARCHAR(100)						NOT NULL,

	CONSTRAINT Tags_PK PRIMARY KEY CLUSTERED ( Id )
);

UPDATE VersionInfo SET DbVersion = '01.00.01.0002';";

				case "01.00.01.0002":
					return
@"INSERT Tags ( Text ) VALUES ( 'Beginner' );
INSERT Tags ( Text ) VALUES ( 'Intermediate' );
INSERT Tags ( Text ) VALUES ( 'Advanced' );

UPDATE VersionInfo SET DbVersion = '01.00.01.0003';";

				case "01.00.01.0003":
					return
@"CREATE TABLE Sessions (
	Id			INTEGER		IDENTITY(1,1)	NOT NULL,
	UserId		INTEGER						NOT NULL,
	Title		VARCHAR(250)					NOT NULL,
	Abstract		VARCHAR(MAX)					NOT NULL,
	Notes		VARCHAR(MAX)					NULL,
	Accepted		BIT							NULL,

	CONSTRAINT Sessions_PK PRIMARY KEY CLUSTERED ( Id ),
	CONSTRAINT Sessions_Users_FK FOREIGN KEY ( UserId ) REFERENCES Users ( Id ) ON UPDATE CASCADE ON DELETE CASCADE
);

CREATE TABLE SessionTags (
	SessionId	INTEGER						NOT NULL,
	TagId		INTEGER						NOT NULL,

	CONSTRAINT SessionTags_PK PRIMARY KEY CLUSTERED ( SessionId, TagId ),
	CONSTRAINT SessionTags_Tags_FK FOREIGN KEY ( TagId ) REFERENCES Tags ( Id ) ON UPDATE CASCADE ON DELETE CASCADE,
	CONSTRAINT SessionTags_Sessions_FK FOREIGN KEY ( SessionId ) REFERENCES Sessions ( Id ) ON UPDATE CASCADE ON DELETE CASCADE
);

UPDATE VersionInfo SET DbVersion = '01.00.01.0004';";

				case "01.00.01.0004":
					return
@"CREATE TABLE AuthTokens (
	Token		UNIQUEIDENTIFIER				NOT NULL,
	UserId		INTEGER						NOT NULL,
	Expires		DATETIME						NOT NULL,

	CONSTRAINT Tokens_PK PRIMARY KEY ( Token ),
	CONSTRAINT Tokens_Users_FK FOREIGN KEY ( UserId ) REFERENCES Users( Id )
);

UPDATE VersionInfo SET DbVersion = '01.00.02.0000';";

				case "01.00.02.0000":
					return
@"CREATE TABLE TimeSlots (
	Id			INTEGER		IDENTITY	(1,1)	NOT NULL,
	StartTime	DATETIME						NOT NULL,
	EndTime		DATETIME						NOT NULL,

	CONSTRAINT TimeSlots_PK PRIMARY KEY ( Id )
);

ALTER TABLE Sessions ADD TimeSlotId INTEGER NULL;
ALTER TABLE Sessions ADD CONSTRAINT Sessions_TimeSlots_FK FOREIGN KEY ( TimeSlotId ) REFERENCES TimeSlots( Id );

UPDATE VersionInfo SET DbVersion = '01.00.02.0001';";

				case "01.00.02.0001":
					return
@"CREATE TABLE Rooms (
	Id			INTEGER		IDENTITY	(1,1)	NOT NULL,
	DisplayName	VARCHAR(	16)					NOT NULL,

	CONSTRAINT Rooms_PK PRIMARY KEY ( Id )
);

ALTER TABLE Sessions ADD RoomId INTEGER NULL;
ALTER TABLE Sessions ADD CONSTRAINT Sessions_Rooms_FK FOREIGN KEY ( RoomId ) REFERENCES Rooms( Id );

UPDATE VersionInfo SET DbVersion = '01.00.02.0002';";

				case "01.00.02.0002":
					return
@"ALTER TABLE Sponsors ALTER COLUMN DisplayName VARCHAR(32) NOT NULL;

UPDATE VersionInfo SET DbVersion = '01.00.02.0003';";

				default:
					return string.Empty;
			}
		}

		private bool RunUpgradeScript( string UpgradeScript ) {
			SqlCommand Command = new SqlCommand();
			Command.Connection = Connection;
			Command.CommandText = UpgradeScript;
			Command.ExecuteNonQuery();
			return true;
		}

		public void Initialize() {
			ConnectToDb();

			do {
				string UpgradeScript = GetUpgradeScript( GetDatabaseVersion() );

				if( string.IsNullOrWhiteSpace( UpgradeScript ) ) break;
				else RunUpgradeScript( UpgradeScript );
			} while( true );

			Connection.Close();

			SponsorLevelDataStore.FillCache().Wait();
		}
	}
}
