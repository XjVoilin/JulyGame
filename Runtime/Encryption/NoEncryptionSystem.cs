using JulyArch;

namespace JulyGame
{
    public class NoEncryptionSystem : SystemBase, IEncryptionSystem
    {
        public byte[] Encrypt(byte[] data) => data;
        public byte[] Decrypt(byte[] encryptedData) => encryptedData;
    }
}
