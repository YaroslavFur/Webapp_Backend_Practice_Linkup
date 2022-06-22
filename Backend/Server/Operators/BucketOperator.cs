using Amazon.S3;
using Amazon.S3.Model;
using Server.Models;

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

        public static async Task<IEnumerable<S3ObjectDtoModel>> GetObjectsFromBucket(string bucketName, string key, IAmazonS3 s3Client)
        {
            var bucketExists = await s3Client.DoesS3BucketExistAsync(bucketName);
            if (!bucketExists)
                throw new Exception("Bucket doesn't exist");

            var request = new ListObjectsV2Request()
            {
                BucketName = bucketName,
                Prefix = key
            };
            var result = await s3Client.ListObjectsV2Async(request);
            var s3Object = result.S3Objects.Select(s =>
            {
                var urlRequest = new GetPreSignedUrlRequest()
                {
                    BucketName = bucketName,
                    Key = s.Key,
                    Expires = DateTime.UtcNow.AddSeconds(60)
                };
                return new S3ObjectDtoModel()
                {
                    Name = s.Key.ToString(),
                    PresignedUrl = s3Client.GetPreSignedURL(urlRequest),
                };
            });
            return s3Object;
        }
    }
}
