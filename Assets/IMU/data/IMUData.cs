using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace IMU.data
{
    /// <summary>
/// 抽象类，定义了IMU数据处理的基本接口
/// </summary>
public abstract class IMUData
{
    // 以IMU的名称为key，每个IMU对应一个数据序列
    protected Dictionary<string, ImuDataSeries> imuDataDict = new Dictionary<string, ImuDataSeries>();

    public IMUData() { }

    /// <summary>
    /// 为指定IMU添加一条数据记录
    /// </summary>
    public abstract void AddRecord(string imuKey, ImuDataRecord record);

    /// <summary>
    /// 删除指定IMU中的数据记录
    /// </summary>
    public abstract bool RemoveRecord(string imuKey, ImuDataRecord record);

    /// <summary>
    /// 获取指定IMU中所有数据记录，按时间戳排序
    /// </summary>
    public abstract IEnumerable<ImuDataRecord> GetRecords(string imuKey);

    /// <summary>
    /// 对指定IMU的数据记录应用旋转变换
    /// </summary>
    public abstract void ApplyRotation(string imuKey, Quaternion rotation);

    /// <summary>
    /// 对指定IMU的数据记录应用时间偏移（延迟/提前）
    /// </summary>
    public abstract void OffsetTime(string imuKey, TimeSpan offset);

    /// <summary>
    /// 从CSV文件加载IMU数据
    /// CSV格式要求：IMUKey,timestamp,acc_x,acc_y,acc_z,rot_x,rot_y,rot_z
    /// timestamp要求是符合DateTime格式的字符串
    /// </summary>
    public abstract void LoadFromCSV(string filePath);

    /// <summary>
    /// 获取所有IMU的所有数据记录，按时间排序（跨IMU）
    /// </summary>
    public abstract IEnumerable<ImuDataRecord> GetAllRecordsSorted();
}

/// <summary>
/// 默认的IMU数据实现，基于字典管理多个IMU数据序列
/// </summary>
public class DefaultIMUData : IMUData
{
    public override void AddRecord(string imuKey, ImuDataRecord record)
    {
        if (!imuDataDict.ContainsKey(imuKey))
        {
            imuDataDict[imuKey] = new ImuDataSeries();
        }
        imuDataDict[imuKey].AddRecord(record);
    }

    public override bool RemoveRecord(string imuKey, ImuDataRecord record)
    {
        if (imuDataDict.ContainsKey(imuKey))
        {
            return imuDataDict[imuKey].RemoveRecord(record);
        }
        return false;
    }

    public override IEnumerable<ImuDataRecord> GetRecords(string imuKey)
    {
        if (imuDataDict.ContainsKey(imuKey))
        {
            return imuDataDict[imuKey].GetRecords();
        }
        return new List<ImuDataRecord>();
    }

    public override void ApplyRotation(string imuKey, Quaternion rotation)
    {
        if (imuDataDict.ContainsKey(imuKey))
        {
            imuDataDict[imuKey].ApplyRotation(rotation);
        }
    }

    public override void OffsetTime(string imuKey, TimeSpan offset)
    {
        if (imuDataDict.ContainsKey(imuKey))
        {
            imuDataDict[imuKey].OffsetTime(offset);
        }
    }

    public override void LoadFromCSV(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError("文件不存在: " + filePath);
            return;
        }

        using (var reader = new StreamReader(filePath))
        {
            // 读取表头
            string headerLine = reader.ReadLine();
            if (string.IsNullOrEmpty(headerLine))
                return;

            // 默认按逗号分割（如果需要更复杂的解析，可使用第三方库）
            string[] headers = headerLine.Split(',');

            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine();
                if (string.IsNullOrEmpty(line))
                    continue;
                string[] parts = line.Split(',');
                if (parts.Length < 8)
                    continue; // 数据不足则跳过

                try
                {
                    // 解析各字段（请确保csv文件的格式与此处一致）
                    string imuKey = parts[0].Trim();
                    DateTime timestamp = DateTime.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);
                    float acc_x = float.Parse(parts[2].Trim(), CultureInfo.InvariantCulture);
                    float acc_y = float.Parse(parts[3].Trim(), CultureInfo.InvariantCulture);
                    float acc_z = float.Parse(parts[4].Trim(), CultureInfo.InvariantCulture);
                    float rot_x = float.Parse(parts[5].Trim(), CultureInfo.InvariantCulture);
                    float rot_y = float.Parse(parts[6].Trim(), CultureInfo.InvariantCulture);
                    float rot_z = float.Parse(parts[7].Trim(), CultureInfo.InvariantCulture);

                    ImuDataRecord record = new ImuDataRecord(timestamp, acc_x, acc_y, acc_z, rot_x, rot_y, rot_z);
                    AddRecord(imuKey, record);
                }
                catch(Exception ex)
                {
                    Debug.LogError("解析数据行失败: " + line + " 错误信息: " + ex.Message);
                }
            }
        }
    }

    public override IEnumerable<ImuDataRecord> GetAllRecordsSorted()
    {
        List<ImuDataRecord> allRecords = new List<ImuDataRecord>();
        foreach (var series in imuDataDict.Values)
        {
            allRecords.AddRange(series.GetRecords());
        }
        allRecords.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
        return allRecords;
    }
}
}