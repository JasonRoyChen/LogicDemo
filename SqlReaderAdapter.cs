using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Xml;
using Vinno.Enums;
using Vinno.Infrastructure;
using Vinno.Infrastructure.Collections;
using Vinno.Infrastructure.Times;
using Vinno.Mode.Interfaces;
using Vinno.Models.Base.ImageStorage;
using Vinno.Models.Base.ProbeAndApplications;
using Vinno.Modules.ClipboardModule.Models;
using Vinno.Modules.ExamInfoModule;

namespace Vinno.SqlRestore
{
    /// <summary>
    /// Read data from Patient database. The data are PatientInfo, ExamInfo, ScanInfo and ClipImage.
    /// </summary>
    public class SqlReaderAdapter
    {
        /// <summary>
        /// Store the tables which are achieved from SQL Server database.
        /// </summary>
        private const int InvalidAge = -1;
        private readonly List<DataTable> _patientTableList = new List<DataTable>();
        private readonly NamedList<IScanNode> _workSheetsOnExam;
        private readonly NamedList<int, IFetalBPSInfoNode> _fetalBPSInfoNodes;

        public SqlReaderAdapter()
        {
            _workSheetsOnExam = new NamedList<IScanNode>();
            _fetalBPSInfoNodes = new NamedList<int, IFetalBPSInfoNode>();
        }

        /// <summary>
        /// Read the database in sql server and input into adapter.
        /// </summary>
        public void ReadSqlServerDataIntoAdapter(string connectString, bool isVet)
        {
            //Initialize.
       
            DataTable patientInfoesTable = new DataTable();
            DataTable examInfoesTable = new DataTable();
            DataTable scanInfoesTable = new DataTable();
            DataTable clipImagesTable = new DataTable();

            //Input connection string into SqlConnection.
            SqlConnection sqlCnt = new SqlConnection(connectString);
            SqlDataAdapter dataAdapter;
            //Start reading database.
            sqlCnt.Open();

            //Read from different tables based on vet mode or not.
            if (!isVet)
            {
                dataAdapter = new SqlDataAdapter("select * from PatientInfoes", sqlCnt);
                dataAdapter.Fill(patientInfoesTable);
            }
            else
            {
                dataAdapter = new SqlDataAdapter("select * from AnimalInfo", sqlCnt);
                dataAdapter.Fill(patientInfoesTable);
            }
            
            dataAdapter = new SqlDataAdapter("select * from ExamInfoes", sqlCnt);
            dataAdapter.Fill(examInfoesTable);

            dataAdapter = new SqlDataAdapter("select * from ScanInfoes", sqlCnt);
            dataAdapter.Fill(scanInfoesTable);

            dataAdapter = new SqlDataAdapter("select * from ClipImages", sqlCnt);
            dataAdapter.Fill(clipImagesTable);

            //Add into list.
            _patientTableList.Add(patientInfoesTable);
            _patientTableList.Add(examInfoesTable);
            _patientTableList.Add(scanInfoesTable);
            _patientTableList.Add(clipImagesTable);
        }

        /// <summary>
        /// Create instance list for patient info.
        /// </summary>
        /// <returns>Returns the patient info list.</returns>
        public IList<IPatientInfo> GetPatientInfoList(bool isVet)
        {
            IList<IPatientInfo> infoList = new List<IPatientInfo>();
            DataTable patientInfoesTable = _patientTableList[0];
            int rowCount = patientInfoesTable.Rows.Count;
            for(int i = 0; i < rowCount; i++)
            {
                var patientInfoRow = patientInfoesTable.Rows[i];
                infoList.Add(CreatePatientInfo(patientInfoRow, isVet));
            }

            return infoList;
        }

        /// <summary>
        /// Create list for exam info.
        /// </summary>
        /// <param name="patientInfoList">Patient info is required while initializing exam info.</param>
        /// <returns>Returns the exam info list.</returns>
        public IList<IExamInfo> GetExamInfoList(IList<IPatientInfo> patientInfoList)
        {
            IList<IExamInfo> infoList = new List<IExamInfo>();
            DataTable examInfoTable = _patientTableList[1];
            var rowCount = examInfoTable.Rows.Count;
            for (int i = 0; i < rowCount; i++)
            {
                var examInfoRow = examInfoTable.Rows[i];
                var patientInfo = patientInfoList.First(p => p.Pkey == (Guid) examInfoRow["Patient_Pkey"]);

                infoList.Add(CreateExamInfo(examInfoRow, patientInfo));
            }

            return infoList;
        }

        /// <summary>
        /// Create list for scan info.
        /// </summary>
        /// <param name="examInfoList">Exam info is required with the scan info initialize.</param>
        /// <param name="patientInfoList">Patient info is required with the scan info initialize.</param>
        /// <returns>Returns the scan info list.</returns>
        public IList<IScanInfo> GetScanInfoList(IList<IExamInfo> examInfoList, IList<IPatientInfo> patientInfoList)
        {
            IList<IScanInfo> infoList = new List<IScanInfo>();

            DataTable scanInfoTable = _patientTableList[2];
            int rowCount = scanInfoTable.Rows.Count;
            for (int i = 0; i < rowCount; i++)
            {
                var scanInfoRow = scanInfoTable.Rows[i];
                var examInfo = examInfoList.First(e => e.ExamId == scanInfoRow["Exam_ExamId"].ToString());
                
                infoList.Add(CreateScanInfo(scanInfoRow, examInfo));
            }

            return infoList;
        }

        /// <summary>
        /// Create list for scan info.
        /// </summary>
        /// <param name="scanInfoList">Clip image requires scan info.</param>
        /// <returns>Returns the list of clip image.</returns>
        public IList<IClipImageSource> GetClipImage(IList<IScanInfo> scanInfoList)
        {
            IList<IClipImageSource> infoList = new List<IClipImageSource>();

            DataTable scanInfoTable = _patientTableList[3];
            int rowCount = scanInfoTable.Rows.Count;
            for (int i = 0; i < rowCount; i++)
            {
                var clipImageRow = scanInfoTable.Rows[i];
                var scanInfo = scanInfoList.First(s => s.ScanId == clipImageRow["Scan_ScanId"].ToString());

                infoList.Add(CreateClipImage(clipImageRow, scanInfo));
            }

            return infoList;
        }

        private IPatientInfo CreatePatientInfo(DataRow patientInfoRow, bool isVet)
        {
            int first = InvalidAge;
            int second = InvalidAge;

            var primaryAge = patientInfoRow["DbPrimaryAge"];
            if (primaryAge != DBNull.Value)
            {
                first = (int)primaryAge;
            }
             
            var secondaryAge = patientInfoRow["DbSecondaryAge"];
            if (secondaryAge != DBNull.Value)
            {
                second = (int)secondaryAge;
            }

            var age = CreateAge((AgeUnits)patientInfoRow["DbAgeUnit"], first, second);

            PatientInfo patientInfo = new PatientInfo((Guid) patientInfoRow["Pkey"])
            {
                PatientId = patientInfoRow["PatientId"].ToString(),
                SecondId = patientInfoRow["SecondId"].ToString(),
                FamilyName = patientInfoRow["FamilyName"].ToString(),
                FirstName = patientInfoRow["FirstName"].ToString(),
                MiddleName = patientInfoRow["MiddleName"].ToString(),
            };
            if (string.IsNullOrEmpty(patientInfoRow["IsMale"].ToString()))
            {
                patientInfo.Gender = GenderType.NotSelected;
            }
            else
            {
                patientInfo.Gender = patientInfoRow["IsMale"].ToString() == "True" ? GenderType.Male : GenderType.Female;
            }

            if (age != null)
            {
                UpdateAge(patientInfo, age);
            }

            var birthDate = patientInfoRow["BirthDay"];
            if (birthDate != DBNull.Value)
            {
                var ageInfo = patientInfo.Age as Age;
                if (ageInfo != null)
                {
                    ageInfo.BirthDay = (DateTime)birthDate;
                }
            }

            var lastDate = patientInfoRow["LastDate"];
            if (lastDate != DBNull.Value)
            {
                var detail = patientInfo.Detail as PatientDetail;
                if (detail != null) detail.LastDate = (DateTime?) lastDate;
            }

            //If not vet, return. Else set value for breed, owner, speciesDb.
            if (!isVet) return patientInfo;
            
            var animalDetail = patientInfo.Detail as AnimalDetail;
            if (animalDetail == null) return patientInfo;

            var breed = patientInfoRow["Breed"];
            if (breed != DBNull.Value)
            {
                animalDetail.Breed = breed.ToString();
            }
            var owner = patientInfoRow["Owner"];
            if (owner != DBNull.Value)
            {
                animalDetail.Owner = owner.ToString();
            }
            var species = patientInfoRow["SpeciesDb"];
            if (species != DBNull.Value)
            {
                animalDetail.Species = (SpeciesType) species;
            }

            return patientInfo;
        }

        private static void UpdateAge(IPatientInfo patientInfo, IAge age)
        {
            var previousAge = patientInfo.Age as Age;
            if (previousAge == null) return;

            previousAge.Year = age.Year;
            previousAge.Month = age.Month;
            previousAge.Week = age.Week;
            previousAge.Day = age.Day;
            previousAge.Unit = age.Unit;
        }

        private static IAge CreateAge(AgeUnits unit, int first, int second)
        {
            switch (unit)
            {
                case AgeUnits.Year:
                {
                    var age = new Age(first, InvalidAge, InvalidAge, InvalidAge, AgeUnits.Year);
                    return age;
                }
                case AgeUnits.YearAndMonth:
                {
                    var age = new Age(first, second, InvalidAge, InvalidAge, AgeUnits.YearAndMonth);
                    return age;
                }
                case AgeUnits.MonthAndWeek:
                {
                    var age = new Age(InvalidAge, first, second, InvalidAge, AgeUnits.MonthAndWeek);
                    return age;
                }
                case AgeUnits.WeekAndDay:
                {
                    var age = new Age(InvalidAge, InvalidAge, first, second, AgeUnits.WeekAndDay);
                    return age;
                }
            }

            return null;
        }

        private IExamInfo CreateExamInfo(DataRow examInfoRow, IPatientInfo patientInfo)
        {
            var examTime = (DateTime) examInfoRow["ExamDate"];
            ExamInfo examInfo = new ExamInfo((PatientInfo)patientInfo, examInfoRow["ExamId"].ToString(), examTime)
            {
                Comment = examInfoRow["Comment"].ToString(),
                ExtraInfo = examInfoRow["ExtraInfo"].ToString(),
                AccessionNumber = examInfoRow["AccessionNumber"].ToString(),

                Operator = examInfoRow["Operator"].ToString(),
                PerformingPhysician = examInfoRow["ExamPhysician"].ToString(),
                ReferringPhysician = examInfoRow["PerfPhysician"].ToString(),
            };

            var eddValue = examInfoRow["OBInfo_EDDByLMP"];
            var gaValue = examInfoRow["OBInfo_GAByLMP"];
            if (eddValue != DBNull.Value && gaValue != DBNull.Value)
            {
                var edd = (DateTime) eddValue;
                var ga = (int) gaValue;
                var total = (edd.ToLocalTime().Date - examTime.ToLocalTime().Date).Days + ga;
                var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                var gaConstraint = examInfo.OBInfo.GetType().GetField("_gaConstraint", flags);
                if (gaConstraint != null)
                {
                    var constraintObj = gaConstraint.GetValue(examInfo.OBInfo);
                    var eddFactor = constraintObj.GetType().GetField("_eddFactor", flags);
                    if (eddFactor != null)
                    {
                        eddFactor.SetValue(constraintObj, total);
                    }
                }
            }
            
            if (examInfoRow["GeneralInfo_Height"] != DBNull.Value)
            {
                examInfo.GeneralInfo.Height = (double) examInfoRow["GeneralInfo_Height"];
            }
            if (examInfoRow["GeneralInfo_Weight"] != DBNull.Value)
            {
                examInfo.GeneralInfo.Weight = (double) examInfoRow["GeneralInfo_Weight"];
            }
            if (examInfoRow["GeneralInfo_BSA"] != DBNull.Value)
            {
                examInfo.GeneralInfo.BSA = (double)examInfoRow["GeneralInfo_BSA"];
            }

            examInfo.GYNInfo.Ectopic = examInfoRow["GYNInfo_Ectopic"].ToString();
            if (examInfoRow["GYNInfo_AB"] != DBNull.Value)
            {
                examInfo.GYNInfo.AB = (int) examInfoRow["GYNInfo_AB"];
            }
            if (examInfoRow["GYNInfo_Gravida"] != DBNull.Value)
            {
                examInfo.GYNInfo.Gravida = (int) examInfoRow["GYNInfo_Gravida"];
            }
            if (examInfoRow["GYNInfo_LMP"] != DBNull.Value)
            {
                examInfo.GYNInfo.LMP = (DateTime) examInfoRow["GYNInfo_LMP"];
            }
            if (examInfoRow["GYNInfo_Para"] != DBNull.Value)
            {
                examInfo.GYNInfo.Para = (int) examInfoRow["GYNInfo_Para"];
            }
            if (examInfoRow["URInfo_PPSACoefficient"] != DBNull.Value)
            {
                examInfo.URInfo.PPSACoefficient = (double) examInfoRow["URInfo_PPSACoefficient"];
            }
            if (examInfoRow["URInfo_PSA"] != DBNull.Value)
            {
                examInfo.URInfo.PSA = (double) examInfoRow["URInfo_PSA"];
            }

            var obInfo = examInfo.OBInfo as PatientOBInfo;
            if (obInfo != null)
            {
                obInfo.GAOrigin = GAOrigins.Convert((int) examInfoRow["OBInfo_GestationalAgeOrigin"]);

                obInfo.Ectopic = examInfoRow["OBInfo_Ectopic"].ToString();
                if (examInfoRow["OBInfo_FetusNumber"] != DBNull.Value)
                {
                    obInfo.FetusNumber = (int) examInfoRow["OBInfo_FetusNumber"];
                }
                if (examInfoRow["OBInfo_AB"] != DBNull.Value)
                {
                    obInfo.AB = (int) examInfoRow["OBInfo_AB"];
                }
                if (examInfoRow["OBInfo_Gravida"] != DBNull.Value)
                {
                    obInfo.Gravida = (int) examInfoRow["OBInfo_Gravida"];
                }
                if (examInfoRow["OBInfo_Para"] != DBNull.Value)
                {
                    obInfo.Para = (int) examInfoRow["OBInfo_Para"];
                }

                if (gaValue != DBNull.Value)
                {
                    obInfo.GestationalDays = (int) gaValue;
                    if (obInfo.GAOrigin == GAOrigins.GA)
                    {
                        //这里转换examInfoRow["OBInfo_GaSourceDate"]到datetime。再做计算
                        obInfo.PrevExamGA = obInfo.GestationalDays - (examInfo.ExamDate.ToLocalTime().Date - ((DateTime)examInfoRow["OBInfo_GaSourceDate"]).ToLocalTime().Date).Days;
                    }
                }

                if(examInfoRow["WorkSheetsXml"] != DBNull.Value)
                {
                    var workSheetXml = (byte[])examInfoRow["WorkSheetsXml"];
                    SetValueForExamWorkSheet(workSheetXml, examInfo);
                    SaveWorksheet(examInfo);
                }
            }

            return examInfo;
        }

        private void SetValueForExamWorkSheet(byte[] value, IExamInfo examInfo)
        {
            CpuClock c = new CpuClock();
            try
            {
                using (_workSheetsOnExam.LockOnCollectionChanged())
                {
                    using (_fetalBPSInfoNodes.LockOnCollectionChanged())
                    {
                        _fetalBPSInfoNodes.Clear();
                        _workSheetsOnExam.Clear();
                        try
                        {
                            if (value != null && value.Length > 0)
                            {
                                ScanNodeContext context = new ScanNodeContext((ExamInfo)examInfo);

                                var extensibleData = ExtensibleData.DeSerialize(value, context);
                                if (extensibleData != null)
                                {
                                    _fetalBPSInfoNodes.AddRange(extensibleData.BpsNodes);
                                    _workSheetsOnExam.AddRange(extensibleData.ScanNodes);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _workSheetsOnExam.Clear();
                            _fetalBPSInfoNodes.Clear();
                            Logger.WriteLineError("Failed to parse ExtensibleData value, details:{0}.", ex);
                        }
                    }
                }
            }
            finally
            {
                if (c.TotalMilliSeconds >= 500)
                {
                    Logger.WriteLineError("ExamInfo.SetWorkSheetsXml spent {0:F2} ms", c.TotalMilliSeconds);
                }
            }
        }

        private void SaveWorksheet(IExamInfo examInfo)
        {
            var exam = examInfo as ExamInfo;
            var scanNodes = exam?.ScanNodes as NamedList<IScanNode>;

            //Do clear in both worksheet & fetalBPS info nodes.
            scanNodes?.Clear();
            exam?.FetalBPSInfoNodes.Clear();

            if (_workSheetsOnExam.Count != 0)
            {
                foreach (var workSheet in _workSheetsOnExam)
                {
                    exam?.AddScanNode(workSheet);
                }
            }
            
            exam?.FetalBPSInfoNodes.AddRange(_fetalBPSInfoNodes);
            exam?.SaveWorksheet();
        }

        private IScanInfo CreateScanInfo(DataRow scanInfoRow, IExamInfo examInfo)
        {
            var time = (DateTime) scanInfoRow["ScanDate"];
            var id = scanInfoRow["ScanId"].ToString();
            var probeName = scanInfoRow["ProbeName"].ToString();
            var app = ApplicationInfo.Unknown;
            var appXml = scanInfoRow["ApplicationInfoXml"].ToString();
            if (!string.IsNullOrEmpty(appXml))
            {
                XmlDocument xDoc = new XmlDocument();
                xDoc.LoadXml(appXml);
                XmlElement element = xDoc.DocumentElement;
                app.ReloadFrom(element);
            }

            ScanInfo scanInfo = new ScanInfo((ExamInfo) examInfo, id, time, probeName, app);
            return scanInfo;
        }

        private IClipImageSource CreateClipImage(DataRow clipImageRow, IScanInfo scanInfo)
        {
            ClipImage clipImage = new ClipImage(clipImageRow["ImageId"].ToString(), (ScanInfo)scanInfo)
            {
                Index = (int)(long)clipImageRow["Index"],
                ActiveArea = (ReferenceAreaEnum)clipImageRow["ActiveArea"],
                ImageType = clipImageRow["ImageType"].ToString(),
                IsCine = (bool)clipImageRow["IsCine"],
                MultiDisplayFormat = (MultiDisplayFormatEnum)clipImageRow["MultiDisplayFormat"],
                ImageSize = (long)clipImageRow["ImageSize"],
                TimeStamp = (DateTime)clipImageRow["TimeStamp"]
            };
            return clipImage;
        }
    }
}
