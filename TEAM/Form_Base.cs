﻿using System.Collections.Generic;
using System.Windows.Forms;

namespace TEAM
{
    public partial class FormBase : Form
    {
        protected FormMain MyParent;

        public FormBase()
        {
            InitializeComponent();
        }
        public FormBase(FormMain myParent)
        {
            MyParent = myParent;
            InitializeComponent();
        }

        // TEAM configuration settings.
        public static TeamConfiguration TeamConfigurationSettings { get; set; } = new TeamConfiguration();

        // TEAM working environment collection.
        public static TeamWorkingEnvironmentCollection TeamEnvironmentCollection { get; set;  } = new TeamWorkingEnvironmentCollection();

        // Table Mapping Metadata.
        public static TeamTableMapping TableMapping { get; set; } = new TeamTableMapping();

        // TEAM Version List.
        public static TeamVersionList EnvironmentVersion { get; set; } = new TeamVersionList();

        /// <summary>
        /// Gets or sets the values for the validation of the metadata.
        /// </summary>
        internal static class ValidationSettings
        {
            // Existence checks (in physical model or virtual representation of it)
            public static string SourceObjectExistence { get; set; }
            public static string TargetObjectExistence { get; set; }
            public static string SourceBusinessKeyExistence { get; set; }
            public static string SourceAttributeExistence { get; set; }
            public static string TargetAttributeExistence { get; set; }

            // Consistency of the unit of work
            public static string LogicalGroup { get; set; }
            public static string LinkKeyOrder { get; set; }

            // Syntax validation
            public static string BusinessKeySyntax { get; set; }
        }

        /// <summary>
        /// These parameters are used as global constants throughout the application.
        /// </summary>
        internal static class GlobalParameters
        {
            internal static EventLog TeamEventLog { get; set; } = new EventLog();

            // TEAM core path parameters
            public static string RootPath { get; } = Application.StartupPath + @"\";
            public static string ConfigurationPath { get; set; } = RootPath + @"Configuration\";
            public static string OutputPath { get; set; } = RootPath + @"Output\";
            public static string CorePath { get; } = RootPath + @"Core\";

            public static string BackupPath { get; } = RootPath + @"Backup\";
            public static string ScriptPath { get; set; } = RootPath + @"Scripts\";
            public static string FilesPath { get; set; } = RootPath + @"Files\";
            internal static string LoadPatternPath { get; set; } = RootPath + @"LoadPatterns\";

            public static string ConfigFileName { get; set; } = "TEAM_configuration";
            public static string PathFileName { get; set; } = "TEAM_Path_configuration";
            public static string ValidationFileName { get; set; } = "TEAM_validation";
            public static string VersionFileName { get; } = "TEAM_versions";
            public static string FileExtension { get; set; } = ".txt";
            internal static string WorkingEnvironment { get; set; } = "Development";
            internal static string WorkingEnvironmentInternalId { get; set; }

            // Json file name parameters
            public static string JsonTableMappingFileName { get; } = "TEAM_Table_Mapping";
            public static string JsonAttributeMappingFileName { get; } = "TEAM_Attribute_Mapping";
            public static string JsonModelMetadataFileName { get; } = "TEAM_Model_Metadata";
            public static string JsonConnectionFileName { get; } = "TEAM_connections";
            public static string JsonEnvironmentFileName { get; } = "TEAM_environments";
            public static string JsonExtension { get;  } = ".json";

            public static string LoadPatternDefinitionFile { get; } = "loadPatternDefinition.json";

            // Version handling
            public static int CurrentVersionId { get; set; } = 0;
            public static int HighestVersionId { get; set; } = 0;

            // File paths
            public static List<LoadPatternDefinition> PatternDefinitionList { get; set; }
        }
    }
}
