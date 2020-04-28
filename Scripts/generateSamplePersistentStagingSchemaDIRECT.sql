IF OBJECT_ID('dbo.HSTG_PROFILER_CUST_MEMBERSHIP', 'U') IS NOT NULL DROP TABLE[dbo].[HSTG_PROFILER_CUST_MEMBERSHIP]
IF OBJECT_ID('dbo.HSTG_PROFILER_CUSTOMER_OFFER', 'U') IS NOT NULL DROP TABLE[dbo].[HSTG_PROFILER_CUSTOMER_OFFER]
IF OBJECT_ID('dbo.HSTG_PROFILER_CUSTOMER_PERSONAL', 'U') IS NOT NULL DROP TABLE[dbo].[HSTG_PROFILER_CUSTOMER_PERSONAL]
IF OBJECT_ID('dbo.HSTG_PROFILER_ESTIMATED_WORTH', 'U') IS NOT NULL DROP TABLE[dbo].[HSTG_PROFILER_ESTIMATED_WORTH]
IF OBJECT_ID('dbo.HSTG_PROFILER_OFFER', 'U') IS NOT NULL DROP TABLE[dbo].[HSTG_PROFILER_OFFER]
IF OBJECT_ID('dbo.HSTG_PROFILER_PERSONALISED_COSTING', 'U') IS NOT NULL DROP TABLE[dbo].[HSTG_PROFILER_PERSONALISED_COSTING]
IF OBJECT_ID('dbo.HSTG_PROFILER_PLAN', 'U') IS NOT NULL DROP TABLE[dbo].[HSTG_PROFILER_PLAN]
IF OBJECT_ID('dbo.HSTG_USERMANAGED_SEGMENT', 'U') IS NOT NULL DROP TABLE[dbo].[HSTG_USERMANAGED_SEGMENT]

CREATE TABLE [HSTG_PROFILER_CUST_MEMBERSHIP]
(
  [OMD_INSERT_MODULE_INSTANCE_ID][int] NOT NULL,
  [OMD_INSERT_DATETIME] [datetime2] (7) NOT NULL,
  [OMD_EVENT_DATETIME] [datetime2] (7) NOT NULL,
  [OMD_RECORD_SOURCE] [varchar] (100) NOT NULL,
  [OMD_SOURCE_ROW_ID] [int] NOT NULL,
  [OMD_CDC_OPERATION] [varchar] (100) NOT NULL,
  [OMD_HASH_FULL_RECORD] [binary] (16) NOT NULL,
  [OMD_CURRENT_RECORD_INDICATOR] [varchar] (1) NOT NULL DEFAULT 'Y',
  [CustomerID] integer NOT NULL,
  [Plan_Code] nvarchar(100) NOT NULL,
  [Start_Date] datetime2(7) NULL,
  [End_Date] datetime2(7) NULL,
  [Status] nvarchar(100) NULL,
  [Comment] nvarchar(100) NULL
  CONSTRAINT [PK_HSTG_PROFILER_CUST_MEMBERSHIP] PRIMARY KEY NONCLUSTERED ([CustomerID] ASC, [Plan_Code] ASC, [OMD_INSERT_DATETIME] ASC, [OMD_SOURCE_ROW_ID] ASC
)
)

CREATE TABLE [HSTG_PROFILER_CUSTOMER_OFFER]
(
  [OMD_INSERT_MODULE_INSTANCE_ID][int] NOT NULL,
  [OMD_INSERT_DATETIME] [datetime2] (7) NOT NULL,
  [OMD_EVENT_DATETIME] [datetime2] (7) NOT NULL,
  [OMD_RECORD_SOURCE] [varchar] (100) NOT NULL,
  [OMD_SOURCE_ROW_ID] [int] NOT NULL,
  [OMD_CDC_OPERATION] [varchar] (100) NOT NULL,
  [OMD_HASH_FULL_RECORD] [binary] (16) NOT NULL,
  [OMD_CURRENT_RECORD_INDICATOR] [varchar] (1) NOT NULL DEFAULT 'Y',
  [CustomerID] integer NOT NULL,
  [OfferID] integer NOT NULL,
  CONSTRAINT [PK_HSTG_PROFILER_CUSTOMER_OFFER] PRIMARY KEY NONCLUSTERED ([CustomerID] ASC, [OfferID] ASC, [OMD_INSERT_DATETIME] ASC, [OMD_SOURCE_ROW_ID] ASC
)
)

CREATE TABLE [HSTG_PROFILER_CUSTOMER_PERSONAL]
(
  [OMD_INSERT_MODULE_INSTANCE_ID][int] NOT NULL,
  [OMD_INSERT_DATETIME] [datetime2] (7) NOT NULL,
  [OMD_EVENT_DATETIME] [datetime2] (7) NOT NULL,
  [OMD_RECORD_SOURCE] [varchar] (100) NOT NULL,
  [OMD_SOURCE_ROW_ID] [int] NOT NULL,
  [OMD_CDC_OPERATION] [varchar] (100) NOT NULL,
  [OMD_HASH_FULL_RECORD] [binary] (16) NOT NULL,
  [OMD_CURRENT_RECORD_INDICATOR] [varchar] (1) NOT NULL DEFAULT 'Y',
  [CustomerID] integer NOT NULL,
  [Given] nvarchar(100) NULL,
  [Surname] nvarchar(100) NULL,
  [Suburb] nvarchar(100) NULL,
  [State] nvarchar(100) NULL,
  [Postcode] nvarchar(100) NULL,
  [Country] nvarchar(100) NULL,
  [Gender] nvarchar(100) NULL,
  [DOB] datetime2(7) NULL,
  [Contact_Number] integer NULL,
  [Referee_Offer_Made] integer NULL,
  CONSTRAINT [PK_HSTG_PROFILER_CUSTOMER_PERSONAL] PRIMARY KEY NONCLUSTERED([CustomerID] ASC, [OMD_INSERT_DATETIME] ASC, [OMD_SOURCE_ROW_ID] ASC
)
)

CREATE TABLE [HSTG_PROFILER_ESTIMATED_WORTH]
(
  [OMD_INSERT_MODULE_INSTANCE_ID][int] NOT NULL,
  [OMD_INSERT_DATETIME] [datetime2] (7) NOT NULL,
  [OMD_EVENT_DATETIME] [datetime2] (7) NOT NULL,
  [OMD_RECORD_SOURCE] [varchar] (100) NOT NULL,
  [OMD_SOURCE_ROW_ID] [int] NOT NULL,
  [OMD_CDC_OPERATION] [varchar] (100) NOT NULL,
  [OMD_HASH_FULL_RECORD] [binary] (16) NOT NULL,
  [OMD_CURRENT_RECORD_INDICATOR] [varchar] (1) NOT NULL DEFAULT 'Y',
  [Plan_Code] nvarchar(100) NOT NULL,
  [Date_effective] datetime2(7) NOT NULL,
  [Value_Amount] numeric(38,20) NULL,
  CONSTRAINT [PK_HSTG_PROFILER_ESTIMATED_WORTH] PRIMARY KEY NONCLUSTERED([Plan_Code] ASC, [Date_effective] ASC, [OMD_INSERT_DATETIME] ASC, [OMD_SOURCE_ROW_ID] ASC
)
)

CREATE TABLE [HSTG_PROFILER_OFFER]
(
  [OMD_INSERT_MODULE_INSTANCE_ID][int] NOT NULL,
  [OMD_INSERT_DATETIME] [datetime2] (7) NOT NULL,
  [OMD_EVENT_DATETIME] [datetime2] (7) NOT NULL,
  [OMD_RECORD_SOURCE] [varchar] (100) NOT NULL,
  [OMD_SOURCE_ROW_ID] [int] NOT NULL,
  [OMD_CDC_OPERATION] [varchar] (100) NOT NULL,
  [OMD_HASH_FULL_RECORD] [binary] (16) NOT NULL,
  [OMD_CURRENT_RECORD_INDICATOR] [varchar] (1) NOT NULL DEFAULT 'Y',
  [OfferID] integer NOT NULL,
  [Offer_Long_Description] nvarchar(100) NULL,
  CONSTRAINT [PK_HSTG_PROFILER_OFFER] PRIMARY KEY NONCLUSTERED([OfferID] ASC, [OMD_INSERT_DATETIME] ASC, [OMD_SOURCE_ROW_ID] ASC
)
)

CREATE TABLE [HSTG_PROFILER_PERSONALISED_COSTING]
(
  [OMD_INSERT_MODULE_INSTANCE_ID][int] NOT NULL,
  [OMD_INSERT_DATETIME] [datetime2] (7) NOT NULL,
  [OMD_EVENT_DATETIME] [datetime2] (7) NOT NULL,
  [OMD_RECORD_SOURCE] [varchar] (100) NOT NULL,
  [OMD_SOURCE_ROW_ID] [int] NOT NULL,
  [OMD_CDC_OPERATION] [varchar] (100) NOT NULL,
  [OMD_HASH_FULL_RECORD] [binary] (16) NOT NULL,
  [OMD_CURRENT_RECORD_INDICATOR] [varchar] (1) NOT NULL DEFAULT 'Y',
  [Member] integer NOT NULL,
  [Segment] nvarchar(100) NOT NULL,
  [Plan_Code] nvarchar(100) NOT NULL,
  [Date_effective] datetime2(7) NOT NULL,
  [Monthly_Cost] numeric(38,20) NULL,
  CONSTRAINT [PK_HSTG_PROFILER_PERSONALISED_COSTING] PRIMARY KEY NONCLUSTERED([Member] ASC, [Segment] ASC, [Plan_Code] ASC, [Date_effective] ASC, [OMD_INSERT_DATETIME] ASC, [OMD_SOURCE_ROW_ID] ASC
)
)

CREATE TABLE [HSTG_PROFILER_PLAN]
(
  [OMD_INSERT_MODULE_INSTANCE_ID][int] NOT NULL,
  [OMD_INSERT_DATETIME] [datetime2] (7) NOT NULL,
  [OMD_EVENT_DATETIME] [datetime2] (7) NOT NULL,
  [OMD_RECORD_SOURCE] [varchar] (100) NOT NULL,
  [OMD_SOURCE_ROW_ID] [int] NOT NULL,
  [OMD_CDC_OPERATION] [varchar] (100) NOT NULL,
  [OMD_HASH_FULL_RECORD] [binary] (16) NOT NULL,
  [OMD_CURRENT_RECORD_INDICATOR] [varchar] (1) NOT NULL DEFAULT 'Y',
  [Plan_Code] nvarchar(100) NOT NULL,
  [Plan_Desc] nvarchar(100) NULL,
  [Renewal_Plan_Code] nvarchar(100) NULL
  CONSTRAINT [PK_HSTG_PROFILER_PLAN] PRIMARY KEY NONCLUSTERED([Plan_Code] ASC, [OMD_INSERT_DATETIME] ASC, [OMD_SOURCE_ROW_ID] ASC
)
)

CREATE TABLE [HSTG_USERMANAGED_SEGMENT]
(
  [OMD_INSERT_MODULE_INSTANCE_ID][int] NOT NULL,
  [OMD_INSERT_DATETIME] [datetime2] (7) NOT NULL,
  [OMD_EVENT_DATETIME] [datetime2] (7) NOT NULL,
  [OMD_RECORD_SOURCE] [varchar] (100) NOT NULL,
  [OMD_SOURCE_ROW_ID] [int] NOT NULL,
  [OMD_CDC_OPERATION] [varchar] (100) NOT NULL,
  [OMD_HASH_FULL_RECORD] [binary] (16) NOT NULL,
  [OMD_CURRENT_RECORD_INDICATOR] [varchar] (1) NOT NULL DEFAULT 'Y',
  [Demographic_Segment_Code] nvarchar(100) NOT NULL,
  [Demographic_Segment_Description] nvarchar(100) NULL,
  CONSTRAINT [PK_HSTG_USERMANAGED_SEGMENT] PRIMARY KEY CLUSTERED([Demographic_Segment_Code] ASC, [OMD_INSERT_DATETIME] ASC, [OMD_SOURCE_ROW_ID] ASC
)
)