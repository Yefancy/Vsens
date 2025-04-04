using System;
using System.Collections.Generic;
using UnityEngine;

namespace IMU.data
{
    /// <summary>
    /// 单个IMU的数据序列，内部保存多个数据记录，并保证按时间戳排序
    /// </summary>
    public class ImuDataSeries
    {
        private List<ImuDataRecord> records = new List<ImuDataRecord>();

        /// <summary>
        /// 添加记录，并按时间戳重新排序
        /// </summary>
        public void AddRecord(ImuDataRecord record)
        {
            records.Add(record);
            records.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
        }

        /// <summary>
        /// 删除记录
        /// </summary>
        public bool RemoveRecord(ImuDataRecord record)
        {
            return records.Remove(record);
        }

        /// <summary>
        /// 获取所有记录
        /// </summary>
        public IEnumerable<ImuDataRecord> GetRecords()
        {
            return records;
        }

        /// <summary>
        /// 应用旋转变换到每一条记录的加速度和旋转向量
        /// </summary>
        public void ApplyRotation(Quaternion rotation)
        {
            for (int i = 0; i < records.Count; i++)
            {
                var rec = records[i];
                // 对加速度向量旋转
                Vector3 acc = new Vector3(rec.AccX, rec.AccY, rec.AccZ);
                Vector3 rotatedAcc = rotation * acc;
                rec.AccX = rotatedAcc.x;
                rec.AccY = rotatedAcc.y;
                rec.AccZ = rotatedAcc.z;

                // 对旋转向量进行变换（假设为欧拉角形式，具体处理方式可根据需求调整）
                Vector3 rotVec = new Vector3(rec.RotX, rec.RotY, rec.RotZ);
                Vector3 rotatedRot = rotation * rotVec;
                rec.RotX = rotatedRot.x;
                rec.RotY = rotatedRot.y;
                rec.RotZ = rotatedRot.z;
            }
        }

        /// <summary>
        /// 对所有记录的时间戳应用时间偏移
        /// </summary>
        public void OffsetTime(TimeSpan offset)
        {
            for (int i = 0; i < records.Count; i++)
            {
                records[i].Timestamp = records[i].Timestamp.Add(offset);
            }
        }
    }

    /// <summary>
    /// 单个IMU数据记录，包含时间戳、加速度以及旋转数据
    /// </summary>
    public class ImuDataRecord
    {
        public DateTime Timestamp { get; set; }
        public float AccX { get; set; }
        public float AccY { get; set; }
        public float AccZ { get; set; }
        public float RotX { get; set; }
        public float RotY { get; set; }
        public float RotZ { get; set; }

        public ImuDataRecord(DateTime timestamp, float accX, float accY, float accZ, float rotX, float rotY, float rotZ)
        {
            Timestamp = timestamp;
            AccX = accX;
            AccY = accY;
            AccZ = accZ;
            RotX = rotX;
            RotY = rotY;
            RotZ = rotZ;
        }

        public override string ToString()
        {
            return $"Time: {Timestamp}, Acc: ({AccX}, {AccY}, {AccZ}), Rot: ({RotX}, {RotY}, {RotZ})";
        }
    }
}