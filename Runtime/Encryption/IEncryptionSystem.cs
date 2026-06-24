namespace JulyGame
{
    public interface IEncryptionSystem
    {
        byte[] Encrypt(byte[] data);
        byte[] Decrypt(byte[] encryptedData);
    }
}
