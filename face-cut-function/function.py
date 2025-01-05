import json
import boto3.session
import cv2
import numpy as np
import uuid

def handler(event, context):
  body = json.loads(event.body)
  queue_message = body.messages[0].details.message.body
  cut_face_data = json.loads(queue_message)

  session = boto3.session.Session()
  s3_client = session.client(
    service_name='s3',
    endpoint_url='https://storage.yandexcloud.net'
  )

  get_object_response = s3_client.get_object(
    Bucket=cut_face_data.bucket_id,
    Key=cut_face_data.object_id
  )
  object = get_object_response['Body'].read()
  object_as_arr = np.frombuffer(object, np.uint8)

  image = cv2.imdecode(object_as_arr, cv2.IMREAD_COLOR)
  face_rect = cut_face_data.face_rec
  face = image[face_rect[1]:face_rect[1]+face_rect[3], face_rect[0]:face_rect[0]+face_rect[2]]
  face_encoded = cv2.imencode('.jpg', face)[1].tobytes()

  s3_client.put_object(
    Bucket='vvot31-faces',
    Key='%s.jpg' % str(uuid.uuid4()),
    Body=face_encoded,
    ContentType='image/jpeg',
    Metadata={
      'object_id': cut_face_data.object_id
    }
  )