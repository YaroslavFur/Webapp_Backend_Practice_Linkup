using Amazon.S3;

namespace Server.Operators
{
    public class BucketOperator
    {
        public static async Task<bool> CreateBucketAsync(string bucketName, IAmazonS3 s3Client)
        {
            var bucketExists = await s3Client.DoesS3BucketExistAsync(bucketName);
            if (bucketExists)
                return false;
            await s3Client.PutBucketAsync(bucketName);
            return true;
        }
    }
}
