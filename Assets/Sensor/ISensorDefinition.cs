namespace Sensor
{
    public interface ISensorDefinition
    {
        string getCsvHeader();
        string getSensorName();
        
        public class DefaultSensorDefinition : ISensorDefinition
        {
            private readonly string name;
            private readonly string header;

            public DefaultSensorDefinition(string name, string header)
            {
                this.name = name;
                this.header = header;
            }

            public string getCsvHeader()
            {
                return header;
            }

            public string getSensorName()
            {
                return name;
            }
        }
        
        public static ISensorDefinition create(string name, string header)
        {
            return new DefaultSensorDefinition(name, header);
        }
    }
}